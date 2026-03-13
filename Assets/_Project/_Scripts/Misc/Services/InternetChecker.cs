using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Net.Http;

namespace Misc.Services
{
    public class InternetChecker
    {
        private static readonly string[] TestUrls = new[]
        {
            "https://clients3.google.com/generate_204",
            "https://www.gstatic.com/generate_204",
            "https://www.apple.com/library/test/success.html"
        };

        public async UniTask<bool> IsAvailableAsync()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Debug.LogWarning("Unity сообщает: нет соединения, выполняем HTTP проверку.");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

            foreach (var url in TestUrls)
            {
                try
                {
                    var response = await client.GetAsync(url).AsUniTask();
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        Debug.Log($"Интернет подтверждён через: {url}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Ошибка проверки {url}: {ex.Message}");
                }
            }

            Debug.LogError("Интернет не доступен после проверки всех URL.");
            return false;
        }
    }
}