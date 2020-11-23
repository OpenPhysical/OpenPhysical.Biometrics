using FaceONNX;
using System;
using System.Drawing;
using System.IO;

namespace FaceLandmarksExtraction
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("FaceONNX: Face landmarks extraction");
            var files = Directory.GetFiles(@"..\..\..\images");
            var path = @"..\..\..\results";

            using var faceDetector = new FaceDetector();
            using var faceLandmarksExtractor = new FaceLandmarksExtractor();
            Directory.CreateDirectory(path);
            
            Console.WriteLine($"Processing {files.Length} images");

            foreach (var file in files)
            {
                using var bitmap = new Bitmap(file);
                var filename = Path.GetFileName(file);
                var faces = faceDetector.Forward(bitmap);
                var pen = new Pen(Color.Yellow, bitmap.Height / 200 + 1);
                Console.WriteLine($"Image: [{filename}] --> detected [{faces.Length}] faces");

                foreach (var face in faces)
                {
                    var points = faceLandmarksExtractor.Forward(bitmap, face);
                    
                    foreach (var point in points)
                    {
                        Imaging.Draw(bitmap, pen, point);
                        bitmap.Save(Path.Combine(path, filename));
                    }
                }
            }

            Console.WriteLine("Done.");
            Console.ReadKey();
        }
    }
}
