# Known-pose projection fixtures

`pose-projections.json` contains geometric test data, not people or synthetic photographs.
It is derived from the Apache-2.0 Google canonical face at revision
`350fbb2100ad531bc110b93aaea23d96af5a5064`.
See `Notices/Google-Face-Geometry-Apache-2.0.txt` in the Facial library for attribution.

Expected pitch, yaw, and roll are the prescribed physical rotations used to construct
these observations. No inverse pose-estimation implementation supplied the expected angles.
For metric coordinates X right, Y up, Z toward camera, rotate by Rz(roll) Ry(yaw) Rx(pitch),
then translate the face 60 cm in front of the camera. The perspective camera has a 63-degree
vertical field of view and square pixels. Project X and Y with the physical perspective
division, using each fixture's actual image aspect ratio. Relative mesh Z is the negative
centered metric Z, scaled by focal length divided by mean face distance, following the
model's weak-perspective depth convention. Ten iris slots repeat vertex zero and are
ignored by the canonical 468-point geometry solver.

Portrait and landscape dimensions test pixel-aspect handling independently. The combined
4/3/2-degree case tests Euler-axis composition; signed single-axis cases test conventions.
The centered 0/-4.9/4.9-degree case has an apparent eye-line tilt greater than five
degrees despite valid physical angles, guarding against a second incompatible 2D pose gate.
