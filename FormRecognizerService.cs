using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;



namespace HandlingAttachmentsBot
{



    public class FormRecognizerService
    {
        private readonly DocumentAnalysisClient _client;
   

        public FormRecognizerService(string endpoint, string apiKey)
        {
            _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
           
        }

        public async Task<string> ExtractTextFromPdfAsync(string pdfUrl)
        {
            // Download the PDF file from the URL
            byte[] pdfBytes;
            using (var httpClient = new HttpClient())
            {
                pdfBytes = await httpClient.GetByteArrayAsync(pdfUrl);
            }

            // Create a MemoryStream from the byte array
            using (var stream = new MemoryStream(pdfBytes))
            {
                // Start the analysis operation
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", stream,null,default);

                // Wait for the operation to complete
                await operation.WaitForCompletionAsync();

                // Get the result
                AnalyzeResult result = operation.Value;

                // Extract text
                StringBuilder extractedText = new StringBuilder();
                foreach (var page in result.Pages)
                {
                    foreach (var line in page.Lines)
                    {
                        extractedText.AppendLine(line.Content);
                    }
                }

                return extractedText.ToString();
            }
        }
    }


}
