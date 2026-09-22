using FaceRecognitionDotNet;
using System.Text.Json;

namespace ClassPluse.Services
{
    public class FaceRecognitionService : IDisposable
    {
        private readonly FaceRecognition? _faceRecognition;
        private readonly string _modelsDirectory;

        public FaceRecognitionService(IWebHostEnvironment env)
        {
            // The models directory where Dlib .dat files are stored
            _modelsDirectory = Path.Combine(env.ContentRootPath, "FaceModels");
            
            // Ensure directory exists
            if (!Directory.Exists(_modelsDirectory))
            {
                Directory.CreateDirectory(_modelsDirectory);
            }

            // NOTE: For this to work, you MUST have the following files in the FaceModels folder:
            // 1. shape_predictor_68_face_landmarks.dat
            // 2. dlib_face_recognition_resnet_model_v1.dat
            
            // Try to initialize only if models exist, to prevent crashing during development if not downloaded yet.
            if (File.Exists(Path.Combine(_modelsDirectory, "shape_predictor_68_face_landmarks.dat")) &&
                File.Exists(Path.Combine(_modelsDirectory, "dlib_face_recognition_resnet_model_v1.dat")))
            {
                _faceRecognition = FaceRecognition.Create(_modelsDirectory);
            }
        }

        /// <summary>
        /// Detects a face in the image and returns its 128-d encoding as a JSON string.
        /// </summary>
        public string? GetFaceEncodingFromImage(byte[] imageBytes)
        {
            if (_faceRecognition == null) 
                throw new InvalidOperationException("Face Recognition models are not loaded. Please download the .dat files into the FaceModels directory.");

            using var ms = new MemoryStream(imageBytes);
            using var bitmap = new System.Drawing.Bitmap(ms);
            using var image = FaceRecognition.LoadImage(bitmap);
            
            // Find faces
            var faceLocations = _faceRecognition.FaceLocations(image).ToArray();
            
            if (faceLocations.Length == 0)
            {
                return null; // No face found
            }

            if (faceLocations.Length > 1)
            {
                throw new Exception("Multiple faces detected. Please ensure only one face is in the frame.");
            }

            // Get encoding for the single face
            var encodings = _faceRecognition.FaceEncodings(image, faceLocations).ToArray();
            
            if (encodings.Length > 0)
            {
                var encodingArray = encodings[0].GetRawEncoding();
                return JsonSerializer.Serialize(encodingArray);
            }

            return null;
        }

        public double GetFaceDistance(string storedEncodingJson, string newEncodingJson)
        {
            if (string.IsNullOrEmpty(storedEncodingJson) || string.IsNullOrEmpty(newEncodingJson))
                return double.MaxValue;

            try
            {
                var storedEncodingArray = JsonSerializer.Deserialize<double[]>(storedEncodingJson);
                var newEncodingArray = JsonSerializer.Deserialize<double[]>(newEncodingJson);

                if (storedEncodingArray == null || newEncodingArray == null) return double.MaxValue;

                // Calculate Euclidean distance between the two 128-d vectors
                double distance = 0;
                for (int i = 0; i < storedEncodingArray.Length; i++)
                {
                    double diff = storedEncodingArray[i] - newEncodingArray[i];
                    distance += diff * diff;
                }
                return Math.Sqrt(distance);
            }
            catch
            {
                return double.MaxValue;
            }
        }

        /// <summary>
        /// Compares a stored encoding (JSON) with a newly captured encoding (JSON).
        /// Returns true if they are the same person (distance < 0.6 is standard).
        /// </summary>
        public bool CompareFaces(string storedEncodingJson, string newEncodingJson, double tolerance = 0.6)
        {
            double distance = GetFaceDistance(storedEncodingJson, newEncodingJson);
            return distance <= tolerance;
        }

        public void Dispose()
        {
            _faceRecognition?.Dispose();
        }
    }
}
