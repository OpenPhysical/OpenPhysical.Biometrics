# SPDX-License-Identifier: GPL-2.0-or-later
"""Independent public vectors: reference libargon2, Python ZIP, cryptography AESGCM."""
import ctypes
import ctypes.util
import hashlib
import io
import json
from pathlib import Path
import struct
import zipfile

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

salt = bytes(range(16))
nonce = bytes(range(16, 28))
password = b"independent public passphrase"
argon = ctypes.CDLL(ctypes.util.find_library("argon2"))
argon.argon2id_hash_raw.argtypes = [ctypes.c_uint32, ctypes.c_uint32, ctypes.c_uint32,
                                  ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p,
                                  ctypes.c_size_t, ctypes.c_void_p, ctypes.c_size_t]
key = ctypes.create_string_buffer(32)
assert argon.argon2id_hash_raw(3, 65536, 4, password, len(password), salt, len(salt), key, 32) == 0
content = b"public non-biometric fixture"
artifact = {"name": "face.bin", "role": "face", "format": "incits385-2004",
            "length": len(content), "sha256": hashlib.sha256(content).hexdigest()}
manifest = {"version": 1, "personId": "public-fixture", "label": "Example",
            "externalId": "", "evidence": {"source": "public test vector"},
            "artifacts": [artifact]}


def seal(name, entries, compressed=False):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_STORED) as archive:
        for path, value in entries:
            info = zipfile.ZipInfo(path, date_time=(2000, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED if compressed else zipfile.ZIP_STORED
            if name == "independent-symlink" and path == "face.bin":
                info.create_system = 3
                info.external_attr = 0xA1FF << 16
            archive.writestr(info, value)
    plain = stream.getvalue()
    if name == "independent-underreported":
        plain = bytearray(plain)
        struct.pack_into("<HH", plain, len(plain) - 14, 2, 2)
        plain = bytes(plain)
    header = b"OPBIOPKG" + struct.pack(">HHBBHIIIQ", 1, 64, 1, 1, 0, 65536, 3, 4, len(plain)) + salt + nonce
    assert len(header) == 64
    result = header + AESGCM(key.raw).encrypt(nonce, plain, header)
    Path(__file__).with_name(name + ".opbio.hex").write_text(result.hex() + "\n")


encoded = json.dumps(manifest, separators=(",", ":")).encode()
seal("independent-valid", [("manifest.json", encoded), ("face.bin", content)])
seal("independent-duplicate", [("manifest.json", encoded), ("face.bin", content), ("FACE.bin", content)])
seal("independent-traversal", [("manifest.json", encoded), ("../face.bin", content)])
seal("independent-hash-mismatch", [("manifest.json", encoded), ("face.bin", content + b"changed")])
seal("independent-extra", [("manifest.json", encoded), ("face.bin", content), ("extra.bin", b"extra")])
seal("independent-symlink", [("manifest.json", encoded), ("face.bin", content)])
seal("independent-underreported", [("manifest.json", encoded), ("face.bin", content)] +
     [(f"extra-{i}.bin", b"") for i in range(1000)])
expanded = b"A" * (1024 * 1024)
large_manifest = dict(manifest)
large_manifest["artifacts"] = [dict(artifact, length=len(expanded), sha256=hashlib.sha256(expanded).hexdigest())]
seal("independent-expanded", [("manifest.json", json.dumps(large_manifest, separators=(",", ":")).encode()),
                              ("face.bin", expanded)], compressed=True)
