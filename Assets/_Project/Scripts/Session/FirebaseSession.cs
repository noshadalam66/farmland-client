using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace FarmLand.Empire.Session
{
    internal sealed class FirebaseSession
    {
        private readonly string apiKey;
        private string idToken;
        private string refreshToken;
        private double expiresAt;

        public FirebaseSession(string apiKey)
        {
            this.apiKey = UnityWebRequest.EscapeURL(apiKey);
        }

        public bool SignedIn => !string.IsNullOrEmpty(refreshToken);

        public IEnumerator SignIn(string email, string password, Action<string> complete)
        {
            var body = new JObject
            {
                ["email"] = email,
                ["password"] = password,
                ["returnSecureToken"] = true,
            };
            return Exchange("https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword",
                "application/json", body.ToString(), "idToken", "refreshToken", "expiresIn", complete);
        }

        public IEnumerator GetToken(bool forceRefresh, Action<string> complete)
        {
            if (!SignedIn)
            {
                complete(null);
                yield break;
            }

            if (!forceRefresh && Time.realtimeSinceStartupAsDouble < expiresAt - 60)
            {
                complete(idToken);
                yield break;
            }

            string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(refreshToken);
            yield return Exchange("https://securetoken.googleapis.com/v1/token",
                "application/x-www-form-urlencoded", body, "id_token", "refresh_token", "expires_in", complete);
        }

        public void SignOut()
        {
            idToken = null;
            refreshToken = null;
            expiresAt = 0;
        }

        private IEnumerator Exchange(string url, string contentType, string body, string tokenField,
            string refreshField, string expiryField, Action<string> complete)
        {
            using var request = new UnityWebRequest(url + "?key=" + apiKey, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                redirectLimit = 0,
                timeout = 15,
            };
            request.SetRequestHeader("Content-Type", contentType);
            yield return request.SendWebRequest();

            string receivedToken = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var result = JObject.Parse(request.downloadHandler.text);
                    string nextToken = (string)result[tokenField];
                    string nextRefresh = (string)result[refreshField];
                    if (!string.IsNullOrEmpty(nextToken) && !string.IsNullOrEmpty(nextRefresh) &&
                        int.TryParse((string)result[expiryField], out int lifetime) && lifetime > 0)
                    {
                        idToken = nextToken;
                        refreshToken = nextRefresh;
                        expiresAt = Time.realtimeSinceStartupAsDouble + lifetime;
                        receivedToken = idToken;
                    }
                }
                catch (Newtonsoft.Json.JsonException) { }
            }

            complete(receivedToken);
        }
    }
}
