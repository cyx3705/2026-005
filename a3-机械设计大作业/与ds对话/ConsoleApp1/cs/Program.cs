using System;
using RestSharp;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs
{
    internal class Program
    {
    //    static string AskDeepSeek(string apiKey, string userMessage)
    //    {
    //        // 设置客户端
    //        var client = new RestClient("https://api.deepseek.com/chat/completions");
    //        var request = new RestRequest();
    //        request.Method = Method.Post;

    //        // 设置请求头
    //        request.AddHeader("Authorization", $"Bearer {apiKey}");
    //        request.AddHeader("Content-Type", "application/json");

    //        // 构建请求体，使用传入的用户消息
    //        string requestBody = $@"{{
    //    ""messages"": [
    //        {{
    //            ""role"": ""user"",
    //            ""content"": ""{userMessage}""
    //        }}
    //    ],
    //    ""model"": ""deepseek-chat"",
    //    ""max_tokens"": 100,
    //    ""stream"": false,
    //    ""top_p"": 0.9,
    //    ""temperature"": 0.3,
    //    ""frequency_penalty"": 0.1,
    //    ""presence_penalty"": 0.1
    //}}";

    //        request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

    //        // 发送请求
    //        Console.WriteLine("正在调用 DeepSeek API...");
    //        var response = client.Execute(request);

    //        if (response.IsSuccessful)
    //        {
    //            // 解析JSON响应，提取纯文本内容
    //            try
    //            {
    //                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response.Content);
    //                if (jsonResponse.TryGetProperty("choices", out JsonElement choices) &&
    //                    choices.GetArrayLength() > 0)
    //                {
    //                    var firstChoice = choices[0];
    //                    if (firstChoice.TryGetProperty("message", out JsonElement message) &&
    //                        message.TryGetProperty("content", out JsonElement content))
    //                    {
    //                        return content.GetString()?.Trim() ?? "API返回内容为空";
    //                    }
    //                }
    //                return "无法解析API响应中的内容";
    //            }
    //            catch (Exception ex)
    //            {
    //                return $"解析响应时出错: {ex.Message}";
    //            }
    //        }
    //        else
    //        {
    //            return $"API请求失败: {response.StatusCode} - {response.ErrorMessage}";
    //        }
    //    }

        // 使用方法示例：
        static void Main(string[] args)
        {
            float α;
            float dD2=340;
            float dD1=100;
            float a;
            α = 180 - (dD2 - dD1) * 57.3f / a;
            Console.WriteLine(α)
        }
    }
}
