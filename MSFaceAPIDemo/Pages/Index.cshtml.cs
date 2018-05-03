using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace MSFaceAPIDemo.Pages
{
    public class IndexModel : PageModel
    {
        List<string> errors = new List<string>();

        private readonly IFaceServiceClient faceServiceClient =
            new FaceServiceClient("API-KEY-HERE", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");
        
        public List<ImageItem> Images { get; set; }

        private IHostingEnvironment _environment;
        private IMemoryCache _memoryCache;

        public IndexModel(IHostingEnvironment environment, IMemoryCache memoryCache)
        {
            _environment = environment;
            _memoryCache = memoryCache;
        }

        public void OnGet()
        {
            Images = GetImages();
        }

        public async Task<JsonResult> OnPostUpload(string photo1, string photo2)
        {
            var data = new PostResult();

            if (photo1 != null && photo2 != null)
            {
                var task1 = UploadAndDetectFaces(photo1);
                var task2 = UploadAndDetectFaces(photo2);
                await Task.WhenAll(new Task[] { task1, task2 });

                if (task1.Result == null || task2.Result == null)
                {
                    data.Error = "Error detecting faces...\n" + String.Join("\n", errors);
                }
                else 
                {
                    var matchResult = await VerifyFaces(task1.Result[0], task2.Result[0]);
                    if (matchResult == null)
                    {
                        data.Error = "Error verifying faces...\n" + String.Join("\n", errors);
                    }
                    else
                    {
                        data.IsIdentical = matchResult.IsIdentical;
                        data.Confidence = matchResult.Confidence;
                    }
                }
            }
            else 
            {
                data.Error = "Please send two images containing a face";
            }

            data.Photo1 = photo1;
            data.Photo2 = photo2;
            return new JsonResult(data);
        }

        private async Task<string> StoreFileAsync(IFormFile photo)
        {
            var filename = Path.GetFileName(photo.FileName);
            var file = Path.Combine(_environment.WebRootPath, "uploads", filename);

            if (System.IO.File.Exists(file))
                System.IO.File.Delete(file);

            using (var fs = new FileStream(file, FileMode.CreateNew))
            {
                await photo.CopyToAsync(fs);
            }

            return Path.Combine(Url.Content("~/uploads"), filename);
        }

        private async Task<VerifyResult> VerifyFaces(Face face1, Face face2)
        {
            try
            {
                return await faceServiceClient.VerifyAsync(face1.FaceId, face2.FaceId);
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }

            return null;
        }

        // Uploads the image file and calls Detect Faces.

        private async Task<Face[]> UploadAndDetectFaces(string filename)
        {
            // The list of Face attributes to return.
            var faceAttributes = new FaceAttributeType[] { };
                
            // Call the Face API.
            try
            {
                var file = Path.Combine(_environment.WebRootPath, "uploads", filename);
                var fileData = System.IO.File.ReadAllBytes(file);

                using (Stream ms = new MemoryStream(fileData))
                {
                    return await faceServiceClient.DetectAsync(ms, returnFaceId: true);
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f)
            {
                errors.Add($"{filename}, {f.ErrorCode}: {f.ErrorMessage}");
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                errors.Add($"{filename}, Error: {e.Message}");
            }

            return null;
        }

        private List<ImageItem> GetImages()
        {
            List<ImageItem> cacheValue;
            var cacheKey = "imageList";

            _memoryCache.TryGetValue(cacheKey, out cacheValue);
            if (cacheValue == null)
            {
                cacheValue = new List<ImageItem>();

                var imageDir = Path.Combine(_environment.WebRootPath, "uploads");
                if (Directory.Exists(imageDir)) 
                {
                    var files = Directory.GetFiles(imageDir);
                    foreach(var file in files)
                    {
                        var name = Path.GetFileName(file);
                        cacheValue.Add(new ImageItem {
                            Url = Path.Combine(Url.Content("~/uploads"), name),
                            Name = name
                        });
                    }

                    _memoryCache.Set(cacheKey, cacheValue);
                }
            }

            return cacheValue;
        }
    }

    public class PostResult
    {
        public string Photo1 { get; set; }
        public string Photo2 { get; set; }
        public string Error{ get; set; }
        public bool IsIdentical { get; set; }
        public double Confidence { get; set; }

    }

    public class ImageItem
    {
        public string Url { get; set; }
        public string Name { get; set; }
    }
}
