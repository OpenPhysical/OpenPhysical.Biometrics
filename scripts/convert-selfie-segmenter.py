#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Convert the pinned Google square Selfie Segmenter to standard ONNX operators.

Use selfie-conversion-requirements.txt in an isolated Python 3.11 environment.
Pass the official TFLite file and output path. Node names may vary between
converter runs; verify original-model probability parity before replacing the
hash-pinned committed model.
"""
import argparse
import hashlib
from pathlib import Path

import onnx
from onnx import helper, numpy_helper
import tf2onnx
from tf2onnx.handler import tf_op


@tf_op("HardSwish")
class NativeHardSwish:
    @classmethod
    def version_14(cls, ctx, node, **kwargs):
        pass


@tf_op("TFL_Convolution2DTransposeBias")
class DeferredTranspose:
    @classmethod
    def version_1(cls, ctx, node, **kwargs):
        pass


def convert(source):
    assert hashlib.sha256(source.read_bytes()).hexdigest() == (
        "191ac9529ae506ee0beefa6b2c945a172dab9d07d1e802a290a4e4038226658b"
    ), "Unrecognized Google model"
    model, _ = tf2onnx.convert.from_tflite(str(source), opset=14)
    constants = {value.name: value for value in model.graph.initializer}
    nodes, replacements = [], 0
    for node in model.graph.node:
        if node.op_type != "TFL_Convolution2DTransposeBias":
            nodes.append(node)
            continue
        replacements += 1
        data, weights, bias = node.input
        filters = numpy_helper.to_array(constants[weights])
        assert filters.shape == (1, 2, 2, 16), "Unexpected transpose convolution"
        model.graph.initializer.append(numpy_helper.from_array(
            filters.transpose(3, 0, 1, 2).copy(), "segment_filter_iohw"
        ))
        nodes.extend([
            helper.make_node("Transpose", [data], ["segment_nchw"], perm=[0, 3, 1, 2], name="segment_to_nchw"),
            helper.make_node("ConvTranspose", ["segment_nchw", "segment_filter_iohw", bias], ["segment_nchw_output"], strides=[2, 2], pads=[0, 0, 0, 0], name="segment_conv_transpose"),
            helper.make_node("Transpose", ["segment_nchw_output"], list(node.output), perm=[0, 2, 3, 1], name="segment_to_nhwc"),
        ])
    assert replacements == 1, "Unexpected custom-op count"
    assert all(not node.domain and not node.op_type.startswith("TFL_") for node in nodes)
    used = {name for node in nodes for name in node.input}
    retained = [value for value in model.graph.initializer if value.name in used]
    del model.graph.initializer[:]
    model.graph.initializer.extend(retained)
    del model.graph.node[:]
    model.graph.node.extend(nodes)
    model.producer_name = "OpenPhysical Google SelfieSegmenter conversion"
    model.producer_version = "1"
    model.graph.name = "GoogleSelfieSegmenterSquareV1"
    onnx.checker.check_model(model, full_check=True)
    return model.SerializeToString(deterministic=True)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
converted = convert(args.source)
args.output.write_bytes(converted)
print(len(converted), hashlib.sha256(converted).hexdigest())
