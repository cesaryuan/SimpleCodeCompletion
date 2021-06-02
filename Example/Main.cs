using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Drawing;
using Z.Expressions;
using SimpleCodeCompletion;
using Newtonsoft.Json.Linq;
using ICSharpCode.AvalonEdit.Editing;
using System.Reflection;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace TextWindowCodeCompletion
{
    class Main
    {
        static readonly HttpClient client = new HttpClient();
        static JArray quickerVarInfo = new JArray();
        static EvalContext evalContext;
        public static void Init(TextEditor textEditor)
        {
            quickerVarInfo = new JArray()
            {
                JObject.FromObject(new {
                    Key="test",
                    Type=0,
                    Desc="",
                    DefaultValue=""
                })
            };
            CodeCompletion.CustomCompletionDataFromAPI = new CustomCompletionDataGetterFromAPI(GetDataFromAPI);
            new CodeCompletion(textEditor, quickerVarInfo);
        }

        public static Type gettype(string s)
        {
            try
            {
                if (evalContext == null)
                {
                    evalContext = new EvalContext();
                    evalContext.UseLocalCache = true;
                }

                //AppHelper.ShowInformation(typestring);
                var type = evalContext.Execute<Type>("typeof(" + s + ")");
                return type;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static async Task<IList<CustomCompletionData>> GetDataFromAPI(string textArea, int offset, char? triggerChar)
        {
            IList<CustomCompletionData> data = new List<CustomCompletionData>();
            JObject jsonBody = JObject.Parse(@"{
    ""CodeBlock"": """",
    ""OriginalCodeBlock"": """",
    ""Language"": ""CSharp"",
    ""Compiler"": ""Net45"",
    ""ProjectType"": ""Console"",
    ""OriginalFiddleId"": ""O5VX2a"",
    ""NuGetPackageVersionIds"": ""73703"",
    ""OriginalNuGetPackageVersionIds"": ""73703"",
    ""TimeOffset"": ""8"",
    ""ConsoleInputLines"": [],
    ""MvcViewEngine"": ""Razor"",
    ""MvcCodeBlock"": {
        ""Model"": """",
        ""View"": """",
        ""Controller"": """"
    },
    ""OriginalMvcCodeBlock"": {
        ""Model"": """",
        ""View"": """",
        ""Controller"": """"
    },
    ""UseResultCache"": false,
    ""FileType"": ""Console"",
    ""Position"": 307
}");
            string leftWrap = @"using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Drawing;
using System.Reflection;
public class Program
{
	public static void Main()
	{";
            string rightWrap = @"}}";
            string declareVar = string.Join("", quickerVarInfo.Select(x => CodeCompletion.quickerVarMetaData[x["Type"].ToString()]["type"].ToString() + " v_" + x["Key"] + ";"));
            string originCode = textArea.Substring(0, offset) + "@#$%" + textArea.Substring(offset);
            string code = ReplaceQuickerVar(originCode);
            string handledCode = leftWrap + declareVar + code + rightWrap;
            int positon = handledCode.IndexOf("@#$%");
            handledCode = handledCode.Remove(positon, 4);
            jsonBody["CodeBlock"] = handledCode;
            jsonBody["OriginalCodeBlock"] = handledCode;
            jsonBody["Position"] = positon;
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://dotnetfiddle.net/Home/GetAutoComplete");
            httpRequestMessage.Content = new StringContent(jsonBody.ToString(), Encoding.UTF8, "application/json");
            try
            {
                var response = await client.SendAsync(httpRequestMessage);
                var responseString = await response.Content.ReadAsStringAsync();
                var Result = JArray.Parse(responseString);
                foreach (var group in Result.OfType<JObject>().Where(x => (int)x["ItemType"] == 2).GroupBy(x => x["Name"].ToString()))
                {
                    var method = group.First(x => true);
                    bool IsGeneric = (bool)method["IsGeneric"];
                    bool IsExtension = (bool)method["IsExtension"];
                    bool IsStatic = (bool)method["IsStatic"];
                    string description = String.Join("\r\n", group.Select(m =>
                    {
                        IEnumerable<JToken> paramss = null;
                        if (IsExtension)
                        {
                            paramss = m["Params"].Skip(1);
                        }
                        else
                            paramss = m["Params"];
                        return ((!IsStatic) ? "static: " : "")
                                + m["Name"]
                                + "("
                                + String.Join(", ", paramss.Select(y => (((bool)y["IsParams"]) ? "params " : "") + y["Type"] + " " + y["Name"]))
                                + "): " + m["Type"];
                    }));

                    string genericPart = (IsGeneric ? " <>" : "");
                    var onedata = new CustomCompletionData()
                    {
                        name = method["Name"] + genericPart,
                        actualText = method["Name"] +
                                    (IsGeneric ? "<>" : "") +
                                    (method["Params"].Count() > 0 ? "($1" : "(") + ")",
                        //priority = 0,
                        description = description,
                    };
                    data.Add(onedata);

                }
                foreach (var prop in Result.OfType<JObject>().Where(x => (int)x["ItemType"] == 0))
                {
                    var onedata = new CustomCompletionData()
                    {
                        name = prop["Name"].ToString(),
                        actualText = prop["Name"].ToString(),
                        //priority = 1,
                        description = prop["Name"] + ": " + prop["Type"],
                        iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/field.png"
                    };
                    data.Add(onedata);
                }
                foreach (var prop in Result.OfType<JObject>().Where(x => (int)x["ItemType"] == 1))
                {
                    var onedata = new CustomCompletionData()
                    {
                        name = prop["Name"].ToString(),
                        actualText = prop["Name"].ToString(),
                        //priority = 1,
                        description = prop["Name"] + ": " + prop["Type"],
                    };
                    data.Add(onedata);
                }
            }
            catch { };
            return data;
        }

        private static string ReplaceQuickerVar(string expression)
        {
            foreach (var keyValuePair in quickerVarInfo)
            {
                string text = "v_" + keyValuePair["Key"];
                if (expression.Contains("{" + keyValuePair["Key"] + "}"))
                {
                    expression = expression.Replace("{" + keyValuePair["Key"] + "}", text);
                }
            }
            return expression;
        }
    }
}
