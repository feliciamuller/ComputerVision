using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Net.Http;

namespace ComputerVision
{
    public class Program
    {
        private static ComputerVisionClient cvClient;

        static async Task Main(string[] args)
        {
            //Configuration for keys in appsettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            string cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
            string cogSvcKey = configuration["CognitiveServiceKey"];

            //Authentication to Azure AI
            ApiKeyServiceClientCredentials credentials = new ApiKeyServiceClientCredentials(cogSvcKey);
            cvClient = new ComputerVisionClient(credentials)
            {
                Endpoint = cogSvcEndpoint
            };

            //Creating path to the root of project
            string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;

            Console.Write("Klistra in en bildlänk: ");
            string imageUrl = Console.ReadLine();
            await AnalyzeImage(imageUrl, projectDirectory);
            await GetThumbnail(imageUrl, projectDirectory);
        }

        static async Task AnalyzeImage(string imageUrl, string projectDirectory)
        {
            Console.WriteLine($"Analyserar din bild: {imageUrl}\n");

            //Functions to be analyzed
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                VisualFeatureTypes.Description,
                VisualFeatureTypes.Tags,
                VisualFeatureTypes.Categories,
                VisualFeatureTypes.Brands,
                VisualFeatureTypes.Objects,
                VisualFeatureTypes.Adult
            };

            //Http get request for the URL
            using (var httpClient = new HttpClient())
            {
                var imageStream = await httpClient.GetStreamAsync(imageUrl);

                //Analyzing the picture and save the data in analysis
                var analysis = await cvClient.AnalyzeImageInStreamAsync(imageStream, features);

                //Get descriptions
                foreach (var caption in analysis.Description.Captions)
                {
                    Console.WriteLine($"Beskrivning: {caption.Text} (tillförlitlighet: {caption.Confidence.ToString("P")})\n");
                }
                //Get tags
                if (analysis.Tags.Count > 0)
                {
                    Console.WriteLine("Taggar:");
                    foreach (var tag in analysis.Tags)
                    {
                        Console.WriteLine($" -{tag.Name} (tillförlitlighet: {tag.Confidence.ToString("P")})");
                    }
                }
                //Get categories
                List<LandmarksModel> landmarks = new List<LandmarksModel>();
                Console.WriteLine("Kategorier:");
                foreach (var category in analysis.Categories)
                {
                    Console.WriteLine($" - {category.Name} (tillförlitlighet: {category.Score.ToString("P")})");
                    //Get land marks
                    if (category.Detail?.Landmarks != null)
                    {
                        foreach (var landmark in category.Detail.Landmarks)
                        {
                            if (!landmarks.Any(item => item.Name == landmark.Name))
                            {
                                landmarks.Add(landmark);
                            }
                        }
                    }
                }
                if (landmarks.Count > 0)
                {
                    Console.WriteLine("Landmärken:");
                    foreach (var landmark in landmarks)
                    {
                        Console.WriteLine($" - {landmark.Name} (tillförlitlighet: {landmark.Confidence.ToString("P")})");
                    }
                }
                //Get brands
                if (analysis.Brands.Count > 0)
                {
                    Console.WriteLine("Varumärken:");
                    foreach (var brand in analysis.Brands)
                    {
                        Console.WriteLine($" - {brand.Name} (tillförlitlighet: {brand.Confidence.ToString("P")})");
                    }
                }
                //Get objects
                if (analysis.Objects.Count > 0)
                {
                    Console.WriteLine("Objekt i bilden:");

                    var imageData = await httpClient.GetByteArrayAsync(imageUrl);

                    using (imageStream = new MemoryStream(imageData))
                    {
                        //Preparing to mark out the objects in the picture
                        Image image = Image.FromStream(imageStream);
                        Graphics graphics = Graphics.FromImage(image);
                        Pen pen = new Pen(Color.Cyan, 3);
                        Font font = new Font("Arial", 16);
                        SolidBrush brush = new SolidBrush(Color.Black);

                        foreach (var detectedObject in analysis.Objects)
                        {
                            Console.WriteLine($" -{detectedObject.ObjectProperty} (tillförlitlighet: {detectedObject.Confidence.ToString("P")})");

                            //Mark out the object
                            var r = detectedObject.Rectangle;
                            Rectangle rect = new Rectangle(r.X, r.Y, r.W, r.H);
                            graphics.DrawRectangle(pen, rect);
                            graphics.DrawString(detectedObject.ObjectProperty, font, brush, r.X, r.Y);
                        }
                        //Empty row to make more space
                        Console.WriteLine("");

                        //Saving the picture
                        string output_file = Path.Combine(projectDirectory, "objects.jpg");
                        image.Save(output_file);
                        Console.WriteLine("Sparar bild med objekt i " + output_file);
                        Console.WriteLine("");

                        //Checking explicit or sensitive content
                        string ratings = $"Bedömningar av känsligt innehåll:\n -Vuxet: {analysis.Adult.IsAdultContent}\n -Racy: {analysis.Adult.IsRacyContent}\n -Blodigt: {analysis.Adult.IsGoryContent}";
                        Console.WriteLine(ratings);
                        Console.WriteLine("");
                    }
                }
            }
        }

        static async Task GetThumbnail(string imageFile, string projectDirectory)
        {
            bool widthSuccess;
            bool heightSuccess;
            int width;
            int height;

            Console.WriteLine("Skapar miniatyrbild. Hur stor vill du att den ska vara?");
            do
            {
                Console.Write("Bredd: ");
                string widthInput = Console.ReadLine();
                widthSuccess = int.TryParse(widthInput, out width);
                if (!widthSuccess)
                {
                    Console.WriteLine("Felaktig inmatning, försök igen.");
                }

            } while (!widthSuccess);
            do
            {
                Console.Write("Höjd: ");
                string heigthInput = Console.ReadLine();
                heightSuccess = int.TryParse(heigthInput, out height);
                if (!heightSuccess)
                {
                    Console.WriteLine("Felaktig inmatning, försök igen.");
                }

            } while (!heightSuccess);

            //Generate a thumbnail
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var imageStream = await httpClient.GetStreamAsync(imageFile);

                    var thumbnailStream = await cvClient.GenerateThumbnailInStreamAsync(width, height, imageStream, true);

                    string thumbnailFileName = Path.Combine(projectDirectory, "thumbnail.jpg");
                    using (Stream thumbnailFile = File.Create(thumbnailFileName))
                    {
                        await thumbnailStream.CopyToAsync(thumbnailFile);
                    }

                    Console.WriteLine($"Sparar miniatyrbild i {thumbnailFileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ett fel inträffade: {ex.Message}");
            }
        }
    }
}
