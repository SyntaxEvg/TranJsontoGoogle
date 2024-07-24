using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using GoogleTranslateApp.Model;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;
using Serilog;
using Serilog.Events;

class Program
{
    public static string GetFileAssemb = Assembly.GetEntryAssembly().Location;
    public static string AppFolder = Program.GetFileAssemb.Substring(0, Program.GetFileAssemb.LastIndexOf('\\') + 1);

    /// <summary>
    /// program  translate document json  KEY ->Value 
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .Build();

        var Loggerpath = Path.Combine(AppFolder, "logs", "logfile.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(Loggerpath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
        IConfiguration config = builder;

        var SourceLanguage = config["TranslationSettings:SourceLanguage"];
        var TargetLanguage = config["TranslationSettings:TargetLanguage"];
        var InputDirectory = config["TranslationSettings:InputDirectory"];
        var OutputDirectory = config["TranslationSettings:OutputDirectory"];
        var ReqExpText = config["TranslationSettings:reqExpText"];
        var Separator = config["TranslationSettings:Separator"];


        //Console.WriteLine("Введите путь к директории с JSON файлами:");
        //string directoryPath = Console.ReadLine();
        string directoryPath = InputDirectory;

        if (InputDirectory != null && InputDirectory == "")
        {
            directoryPath = Path.Combine(AppFolder, "json");
        }
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);


        string outputDirectory = OutputDirectory;
        if (OutputDirectory != null && OutputDirectory == "")
        {
            outputDirectory = Path.Combine(AppFolder, "json");
        }
        if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);


        string sourceLanguage = SourceLanguage;
        if (SourceLanguage != null && SourceLanguage == "")
        {
            sourceLanguage = "en";
        }
        string targetLanguage = TargetLanguage;
        if (TargetLanguage != null && TargetLanguage == "")
        {
            targetLanguage = "ru";
        }

        string reqExpText = ReqExpText;
        if (reqExpText != null && reqExpText == "")
        {
            reqExpText = "ru_*.json";
        
        }
        string separator = Separator;
        if (separator != null && separator == "")
        {
            separator = "$#";
        }

        string[] jsonFiles = Directory.GetFiles(directoryPath, reqExpText);

        foreach (string file in jsonFiles)
        {
            await ProcessFile(file, sourceLanguage, targetLanguage, separator);
            Log.Warning($"File Translate: {file}");
            await Task.Delay(2000);
        }
        Log.Warning("Translation of all files is completed");
        //Console.ReadLine();
    }
    static string[] SplitTextIntoChunks(string text, int maxChunkSize)
    {
        List<string> chunks = new List<string>();
        int startIndex = 0;

        while (startIndex < text.Length)
        {
            int endIndex = Math.Min(startIndex + maxChunkSize, text.Length);
            chunks.Add(text.Substring(startIndex, endIndex - startIndex));
            startIndex = endIndex;
        }

        return chunks.ToArray();
    }
    static async Task ProcessFile(string filePath, string sourceLanguage, string targetLanguage, string separator)
    {
        string outputPath = filePath;
        string json = await File.ReadAllTextAsync(filePath);
        var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        string[] textToTranslate = SplitTextIntoChunks(string.Join(separator, jsonObject.Values), 2000);
        List<string> translatedText = new List<string>();

        foreach (string chunk in textToTranslate)
        {
            string translatedChunk = await TranslateText(chunk, sourceLanguage, targetLanguage);
            translatedText.Add(translatedChunk);
        }

        string[] translatedValues = string.Concat(translatedText).Split(new[] { separator }, StringSplitOptions.None);

        if (translatedValues != null && translatedValues.Count() == jsonObject.Count())
        {
            var translatedDict = jsonObject.Keys.Zip(translatedValues, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            string outputJson = JsonConvert.SerializeObject(translatedDict, Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, outputJson);
        }
        else
        {
            Log.Error($"File Error: {filePath}");
            Console.WriteLine();
        }
    }
    static async Task<string> TranslateText(string text, string sourceLanguage, string targetLanguage)
    {
        try
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLanguage}&tl={targetLanguage}&dt=t&q={HttpUtility.UrlEncode(text)}";
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (var reader = new JsonTextReader(new StringReader(jsonResponse)))
                    {
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.StartArray)
                            {
                                var collection = ParseJsonArray(reader);
                                foreach (var items in collection)
                                {
                                    var tflag = items is List<object>;
                                    if (tflag)
                                    {
                                        var coll = items as List<object>;
                                        foreach (var item in coll)
                                        {
                                            tflag = item is List<object>;
                                            if (tflag)
                                            {
                                                coll = item as List<object>;
                                                foreach (var itemTrans in coll)
                                                {
                                                    return itemTrans.ToString();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            
                        }
                    }
                    return null;
                }
                else
                {
                    Console.WriteLine($"Translation failed with status code: {response.StatusCode}");
                }
                return null;
            }
        }
        catch (Exception){}return null;
    }
    public static List<object> ParseJsonArray(JsonReader reader)
    {
        var list = new List<object>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    list.Add(JObject.Load(reader));
                    break;
                case JsonToken.StartArray:
                    list.Add(ParseJsonArray(reader));
                    break;
                case JsonToken.Integer:
                    list.Add(reader.Value);
                    break;
                case JsonToken.Float:
                    list.Add(reader.Value);
                    break;
                case JsonToken.String:
                    list.Add(reader.Value);
                    break;
                case JsonToken.Boolean:
                    list.Add(reader.Value);
                    break;
                case JsonToken.Null:
                    list.Add(null);
                    break;
                case JsonToken.EndArray:
                    return list;
            }
        }

        return list;
    }

}