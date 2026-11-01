# Portrait regression fixtures

`glenn.jpg` is NASA's historical portrait of John H. Glenn Jr. It was imported from the existing FaceOFFx `tests/test-images/people/glenn/source.jpg` fixture. Credit: NASA. NASA image-use guidance: https://www.nasa.gov/nasa-brand-center/images-and-media/.

`bush.jpg` is the official 2003 White House portrait of George W. Bush by Eric Draper, imported from the existing FaceOFFx portrait fixture. The Library of Congress catalogs the photograph at https://www.loc.gov/pictures/item/2011645073/. The U.S. government photograph is public domain; its source terms are separate from the project's code license.

The images exercise geometry and encoding. Fixed annotations in the tests are regression inputs for the reviewed-coordinate contract. Acquisition metadata in these tests is synthetic.

`manifest.json` records the size and SHA-256 of each image and reference output. `glenn.input.f32` is little-endian float32 NCHW RGB `[1,3,256,256]`; `glenn.output0.f32` contains the network's `[1,478,3]` output. `glenn.reference.json` retains source-coordinate landmarks, detector keypoints, and probabilities from the pinned upstream implementation.

To reproduce the vectors, install `scripts/portrait-reference-requirements.txt` in a Python 3.11 virtual environment. Check out the yakhyo repository at the revision in the manifest, then run:

```sh
python scripts/generate-portrait-reference.py /path/to/mediapipe-face-mesh-onnx
```

The generator verifies the source revision and model hashes before running CPU inference. Tests allow 0.002 model pixels for direct network parity and 2 source pixels for the full pipeline, which uses independently implemented decoding and interpolation.

`maddie.jpeg` is the repository's existing user-supplied photograph of Maddie the cat, retained as a non-human negative control. The fixture remains subject to the original photograph's rights.
