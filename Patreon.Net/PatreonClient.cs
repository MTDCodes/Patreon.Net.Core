using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JsonApiSerializer;
using JsonApiSerializer.JsonApi;
using Newtonsoft.Json;
using Patreon.Net.Models;

namespace Patreon.Net
{
    public class PatreonClient : IDisposable
    {
        public const string SafeRoot = "https://www.patreon.com/api/oauth2/v2/";
        public const string PublicRoot = "https://www.patreon.com/api/";

        public static string CampaignUrl(string campaignId) => SafeRoot + $"campaigns/{campaignId}";
        public static string PledgesUrl(string campaignId) => CampaignUrl(campaignId) + "/pledges";
        public static string MembersUrl(string campaignId) => CampaignUrl(campaignId) + "/members";
        public static string MemberUrl(string memberId) => SafeRoot + $"members/{memberId}";
        public static string UserUrl(string userId) => PublicRoot + "user/" + userId;

        private readonly HttpClient _httpClient;

        public PatreonClient(string accessToken)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        }

        static string GenerateFieldsAndIncludes(Type includes, params Type[] fields)
        {
            var str = new StringBuilder();

            foreach (var field in fields)
            {
                GenerateFields(field, str);
                str.Append("&");
            }

            GenerateIncludes(includes, str);

            return str.ToString();
        }

        static void GenerateFields(Type type, StringBuilder str)
        {
            str.Append("fields%5B");

            var name = type.Name.Replace("Attributes", "");

            for(int i = 0; i < name.Length; i++)
            {
                var ch = name[i];

                if (char.IsUpper(ch) && i != 0)
                    str.Append("_");

                str.Append(char.ToLower(ch));
            }

            str.Append("%5D=");

            GenerateFieldList(type, str);
        }

        static void GenerateIncludes(Type type, StringBuilder str)
        {
            str.Append($"include=");

            GenerateFieldList(type, str);
        }

        static void GenerateFieldList(Type type, StringBuilder str)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attributes = property.GetCustomAttributes(typeof(JsonPropertyAttribute), true);

                if (attributes.Length == 0)
                {
                    continue;
                }

                foreach (var attribute in attributes)
                {
                    str.Append(((JsonPropertyAttribute)attribute).PropertyName);
                    str.Append(",");
                }
            }

            // remove the last comma
            str.Length -= 1;
        }

        public static string AppendQuery(string url, string query)
        {
            if (url.Contains("?"))
                url += "&" + query;
            else
                url += "?" + query;

            return url;
        }

        public async Task<HttpResponseMessage> Get(string url) => await _httpClient.GetAsync(url);

        public async Task<T> Get<T>(string url)
            where T : class
        {
            var response = await Get(url);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var settings = new JsonApiSerializerSettings();
                    return JsonConvert.DeserializeObject<T>(json, settings);
                }
                catch(Exception ex)
                {
#if DEBUG
                    Console.WriteLine(ex.ToString());
#endif
                }
            }

            return null;
        }

        public async Task<Campaign> GetCampaign(string campaignId)
        {
            var url = CampaignUrl(campaignId);

            url = AppendQuery(url, GenerateFieldsAndIncludes(typeof(CampaignRelationships), 
                typeof(CampaignAttributes), typeof(UserAttributes), typeof(TierAttributes)));

            var document = await Get<DocumentRoot<Campaign>>(url).ConfigureAwait(false);

            return document.Data;
        }

        public async Task<List<Tier>> GetCampaignTiers(string campaignId)
        {
            var campaign = await GetCampaign(campaignId).ConfigureAwait(false);

            return campaign.Relationships.Tiers;
        }

        public async Task<List<Member>> GetCampaignMembers(string campaignId)
        {
            var list = new List<Member>();

            string next = MembersUrl(campaignId);

            do
            {
                var url = next;

                url = AppendQuery(url, GenerateFieldsAndIncludes(typeof(MemberRelationships),
                    typeof(MemberAttributes), typeof(UserAttributes)));

                var document = await Get<DocumentRoot<Member[]>>(url).ConfigureAwait(false);

                list.AddRange(document.Data);

                if (document.Links != null && document.Links.ContainsKey("next"))
                    next = document.Links["next"].Href;
                else
                    next = null;

            } while (next != null);

            return list;
        }

        public async Task<User> GetUser(string id) => (await Get<UserData>(UserUrl(id)).ConfigureAwait(false))?.User;

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
