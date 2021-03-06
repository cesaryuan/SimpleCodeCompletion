using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;

namespace SimpleCodeCompletion
{
    public class CodeCompletion
    {
        #region Quicker
        public static IHighlightingDefinition DescriptionHighlighting;
        static ResourceManager rm = new ResourceManager("SimpleCodeCompletion.Resource1", Assembly.GetExecutingAssembly());

        public static readonly JObject quickerVarMetaData = JObject.Parse(@"{""0"": {""name"": ""Text"",""type"": ""string""},""1"": {""name"": ""Number"",""type"": ""double""},""2"": {""name"": ""Boolean"",""type"": ""bool""},""3"": {""name"": ""Image"",""type"": ""Bitmap""},""4"": {""name"": ""List"",""type"": ""List<string>""},""6"": {""name"": ""DateTime"",""type"": ""DateTime""},""7"": ""Keyboard"",""8"": ""Mouse"",""9"": ""Enum"",""10"": {""name"": ""Dict"",""type"": ""Dictionary<string, object>""},""11"": ""Form"",""12"": {""name"": ""Integer"",""type"": ""int""},""98"": {""name"": ""Object"",""type"": ""Object""},""99"": {""name"": ""Object"",""type"": ""Object""},""100"": ""NA"",""101"": ""CreateVar""}");
        /// <summary>
        /// 自动补全时支持的类型
        /// </summary>
        public static Type[] PredefindTypes = new Type[]
        {
            typeof(List<string>),
            typeof(Dictionary<string, object>),
            typeof(int),
            typeof(double),
            typeof(bool),
            typeof(char),
            typeof(string),
            typeof(String),
            typeof(DateTime),
            typeof(Path),
            typeof(File),
            typeof(Directory),
            typeof(Regex),
            typeof(Convert),
            typeof(JObject),
            typeof(JArray),
            typeof(Bitmap),
            typeof(Enumerable),
            typeof(TimeSpan),
            typeof(JsonConvert),
            typeof(object),
            typeof(Environment),
            typeof(FileInfo),
            typeof(StringComparison),
            typeof(StringSplitOptions),
            typeof(RegexOptions),
            typeof(Assembly),
            typeof(Type),
            typeof(Match)
        };
        /// <summary>
        /// 对符，默认有括号、单引号、双引号
        /// </summary>
        public static List<string[]> PairChars = new List<string[]>(){
                    new string[]{"\"", "\"" },
                    new string[]{"'", "'" },
                    new string[]{"[", "]" },
                    new string[]{"(", ")" },
                };
        /// <summary>
        /// 补全项目匹配函数。输入：string text, string query, 输出：匹配度
        /// </summary>
        public static CustomMatchQualityGetter CustomGetMatchQualityFunc = null;
        /// <summary>
        /// 自定义Snippet，有内置几个常用的，比如if和for结构
        /// </summary>
        public static List<CustomCompletionData> CustomSnippets = JsonConvert.DeserializeObject<List<CustomCompletionData>>(rm.GetString("Snippets"));
        /// <summary>
        /// 定义从网络API获取补全数据的函数，该函数只会在简易补全不生效和主动触发补全的使用
        /// </summary>
        public static CustomCompletionDataGetterFromAPI CustomCompletionDataFromAPI = null;

        public static CustomVarTypeGetter TypeGetter = null;


        CompletionWindow completionWindow;
        JArray quickerVarInfo = new JArray();
        Dictionary<string, List<Type>> CustomVarTypeDefine = new Dictionary<string, List<Type>>();
        private bool IsClosingBrackets;

        /// <summary>
        /// 为 <see cref="ICSharpCode.AvalonEdit.TextEditor"/> 实现一个简单的C#自动补全
        /// </summary>
        /// <param name="textEditor">要绑定的 TextEditor 实例</param>
        /// <param name="QuickerVarInfo">Quicker的动作变量信息转换的JArray</param>
        /// <param name="CustomVarTypeDefine">键为变量名，值为其对应的变量类型，用于为一些特殊变量指定类型，比如 _eval 变量的变量类型为 EvalContext</param>
        public CodeCompletion(
            TextEditor textEditor,
            JArray QuickerVarInfo = null,
            Dictionary<string, List<Type>> CustomVarTypeDefine = null)
        {
            if (QuickerVarInfo != null)
                this.quickerVarInfo = QuickerVarInfo;
            if (CustomVarTypeDefine != null)
                this.CustomVarTypeDefine = CustomVarTypeDefine;

            textEditor.TextArea.TextEntered += TextEnteredHandler;
            textEditor.TextArea.TextEntering += TextEnteringHandler;
            textEditor.KeyDown += OnKeyDown;
            using (StringReader sr = new StringReader(CodeCompletion.rm.GetString("DescriptionHighlight")))
            using (XmlReader xmlReader = XmlReader.Create(sr))
            {
                DescriptionHighlighting = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
            }
        }

        private void TextEnteringHandler(object sender, TextCompositionEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Text) && completionWindow != null)
            {
                // 计划当补全窗口无可用项目时就关闭，但是好像不生效
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    completionWindow.Close();
                    return;
                }

            }
            //if (!char.IsLetterOrDigit(e.Text[0]))
            //{
            //	// Whenever a non-letter is typed while the completion window is open,
            //	// insert the currently selected element.
            //	completionWindow.CompletionList.RequestInsertion(e);
            //}
            // do not set e.Handled=true - we still want to insert the character that was typed
        }


        private void TextEnteredHandler(object sender, TextCompositionEventArgs e)
        {
            if (completionWindow != null)
                return;
            TextArea textArea = sender as TextArea;
            ShowComletion(textArea, e.Text, TriggerMode.Passive);
        }

        private void ShowComletion(TextArea textArea, string triggerChar, TriggerMode triggerMode)
        {
            if (triggerChar == ".")
            {
                DotComletion(textArea, triggerMode);
            }
            else if (triggerChar == "{")
            {
                completionWindow = GetCompletionWindow(textArea);
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                GetDataFromQuickerVars(data);


                if (data.Count() > 0)
                {
                    completionWindow.Show();
                }
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            }
            else if (char.IsLetterOrDigit(triggerChar[0]))
            {
                if (completionWindow == null)
                {
                    if (triggerMode != TriggerMode.Active && char.IsLetterOrDigit(textArea.GetAheadText(2)[0]))
                        return;
                    LetterCompletion(textArea, triggerMode);
                }
            }
            else if (PairChars.Any(x => x[0] == triggerChar || x[1] == triggerChar))
            {
                if (PairChars.Any(x => x[1] == triggerChar) && this.IsClosingBrackets)
                {
                    if (textArea.GetBehindText(1) == triggerChar)
                    {
                        //textArea.RemoveBehindText(1);
                        textArea.Document.UndoStack.Undo();
                        textArea.Caret.Offset++;
                        this.IsClosingBrackets = false;
                    }
                }
                else if (PairChars.Any(x => x[0] == triggerChar))
                {
                    //textArea.Selection.ReplaceSelectionWithText("\"");
                    textArea.Document.Insert(textArea.Caret.Offset, PairChars.First(x => x[0] == triggerChar)[1]);
                    textArea.Caret.Offset -= 1;
                    this.IsClosingBrackets = true;
                }

            }
        }

        private void LetterCompletion(TextArea textArea, TriggerMode triggerMode)
        {
            string token = GetToken(textArea);
            if (token != "")
            {
                // 获取token的parent
                string parent = GetParent(textArea);

                // 生成补全窗口实例
                completionWindow = GetCompletionWindow(textArea, token);
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

                // 从数据库里寻找匹配到的补全条目
                GetDataFromSnippets(parent, token, data);
                if (parent == "")
                {
                    GetDataFromPossibleVarNames(token, textArea.TextView.Document.Text, data);
                    GetDataFromPredefindTypes(token, data);
                    GetDataFromPredefindKeyWords(token, data);
                }
                if (triggerMode == TriggerMode.Active)
                    GetDataFromAPI(textArea, data);
                GetDataFromReflection(textArea, data);
                // 补全数据不为空，则显示补全窗口
                if (data.Count() > 0)
                {
                    completionWindow.Show();
                }

                // 绑定退出事件
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            }
        }

        private void DotComletion(TextArea textArea, TriggerMode triggerMode)
        {
            var parent = GetParent(textArea);
            completionWindow = GetCompletionWindow(textArea);
            IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

            GetDataFromSnippets(parent, "", data);

            if (!GetDataFromReflection(textArea, data) || triggerMode == TriggerMode.Active)
            {
                GetDataFromAPI(textArea, data);
            }
            // 补全数据不为空，则显示补全窗口
            if (data.Count() > 0)
            {
                completionWindow.Show();
            }
            completionWindow.Closed += delegate
            {
                completionWindow = null;
            };
        }

        public void OnKeyDown(object o, KeyEventArgs e)
        {
            TextArea textArea = ((TextEditor)o).TextArea;
            if (e.Key == Key.Space && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;

                if (completionWindow == null)
                {
                    var fchar = textArea.GetAheadText(1);
                    ShowComletion(textArea, fchar, TriggerMode.Active);
                }
            }
        }

        private CompletionWindow GetCompletionWindow(TextArea textArea, string text = "")
        {
            var completionWindow = new CompletionWindow(textArea);
            completionWindow.Width = 230;
            completionWindow.CompletionList.InsertionRequested += (ss, ee) =>
            {
                this.IsClosingBrackets = true;
            };
            completionWindow.CustomGetMatchQualityFunc = (itemText, query) =>
            {
                return MatchQuery(itemText, text + query);
            };
            completionWindow.CloseAutomatically = true;

            return completionWindow;
        }

        private void GetDataFromPredefindKeyWords(string token, IList<ICompletionData> data)
        {
            var keywords = new List<string>()
            {
                "return",
                "var",
                "new",
                "while",
                "break",
                "throw",
                "private",
                "enum",
                "delegate",
                "default",
                "static",
                "false",
                "true",
                "struct",
                "case",
                "typeof",
                "sizeof",
                "nameof",
                "using"
            };
            foreach (var item in keywords.Where(x => x.StartsWith(token, StringComparison.OrdinalIgnoreCase)).Select(x =>
                    new CustomCompletionData()
                    {
                        name = x,
                        actualText = x,
                        priority = 20,
                        iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/keywords.png"
                    }))
            {
                if (item != null)
                {
                    item.replaceOffset = token.Length;
                    data.Add(item);
                }
            }
        }

        private async void GetDataFromAPI(TextArea textArea, IList<ICompletionData> data)
        {
            if (CustomCompletionDataFromAPI != null)
            {
                var allCode = textArea.Document.Text;
                var offset = textArea.Caret.Offset;

                foreach (var item in await CustomCompletionDataFromAPI(allCode, offset, null))
                {
                    data.Add(item);
                }
            }
        }



        private void GetDataFromQuickerVars(IList<ICompletionData> data)
        {
            foreach (var item in quickerVarInfo.OfType<JObject>())
            {
                if (item != null)
                {
                    var varName = item["Key"].ToString();
                    var DefaultValue = item["DefaultValue"].ToString();
                    var Desc = item["Desc"].ToString();
                    var temp = new CustomCompletionData()
                    {
                        name = varName,
                        actualText = varName + "}",
                        description = "介绍：" + Desc +
                                        (DefaultValue.Length < 10000 ? "\r\n" + "默认值：" + DefaultValue : ""),
                        iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/" + quickerVarMetaData[item["Type"].ToString()]["name"].ToString().ToLower() + ".png",
                    };
                    data.Add(temp);
                }
            }
            //data.Add(new CustomCompletionData()
            //{
            //    name = "quicker_in_param",
            //    actualText = "quicker_in_param}",
            //    replaceOffset = 0,
            //    description = "介绍：保存有传入动作的参数",
            //    iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/text.png",
            //});
        }

        private void GetDataFromPredefindTypes(string token, IList<ICompletionData> data)
        {
            foreach (var item in PredefindTypes.Where(x => x.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase)))
            {
                var name = ValueTypeHandle(item.Name);
                var temp = new CustomCompletionData()
                {
                    name = name,
                    actualText = item.IsGenericType ? Regex.Replace(name, @"`\d+$", "<$1>") : name,
                    priority = 10,
                    description = name,
                    iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/class.png"
                };
                if (item != null)
                {
                    temp.replaceOffset = token.Length;
                    data.Add(temp);
                }
            }
        }

        private void GetDataFromSnippets(string parent, string token, IList<ICompletionData> data)
        {
            if (CustomSnippets == null)
                return;
            var completionData = CustomSnippets.Where(x => x.name.StartsWith(token, StringComparison.OrdinalIgnoreCase) && (x.parent.Split('|').Any(y => y.Equals(parent, StringComparison.OrdinalIgnoreCase)) || (parent != "" && x.parent == ".")));
            foreach (var item in completionData)
            {
                if (item != null)
                {
                    item.replaceOffset = token.Length;
                    data.Add(item);
                }
            }
        }

        private bool GetDataFromReflection(TextArea textArea, IList<ICompletionData> data)
        {
            bool flag = false;
            var parent = GetParent(textArea);
            string allCode = GetCodeBeforeCaret(textArea);
            List<Type> temp = new List<Type>();
            var varName = parent.TrimEnd('.');
            temp.AddRange(GetParentPossibleTypes(varName, allCode));


            foreach (var item in GetMethods(temp, BindingFlags.Instance | BindingFlags.Public, true).Concat(GetPropertys(temp)))
            {
                flag = true;
                data.Add(item);
            }

            // 获取静态方法
            temp = GetTypeWithString(parent.TrimEnd('.'));
            // AppHelper.ShowInformation(parent);
            foreach (var item in GetMethods(temp, BindingFlags.Static | BindingFlags.Public, false).Concat(GetPropertys(temp)).Concat(GetFields(temp)))
            {
                flag = true;
                data.Add(item);
            }
            return flag;
        }

        private bool GetDataFromPossibleVarNames(string token, string allCode, IList<ICompletionData> data)
        {
            string typePattern = @"(?<=^(\$=)?\s*|foreach *\()(?<type>(?<![<>,\w])[a-zA-Z][<>, \w]+)";
            string space = @" +";
            string varPattern = @"(?<!\w)(?<var>[a-zA-Z]\w*)";
            var pattern = new Regex(String.Format(@"{0}{2}{1}\s*(;|=|in)", typePattern, varPattern, space), RegexOptions.Multiline);
            var matches = pattern.Matches(allCode);
            foreach (var group in matches.OfType<Match>().GroupBy(x => x.Groups["var"].Value + x.Groups["type"].Value))
            {
                var item = group.First(x => true);
                string varName = item.Groups["var"].Value;
                if (varName.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    data.Add(new CustomCompletionData()
                    {
                        name = varName,
                        replaceOffset = token.Length,
                        description = item.Groups["type"].Value,
                        iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/globalvar.png"
                    });
                }
            }
            if (data.Count > 0)
                return true;
            return false;
        }

        private List<Type> GetParentPossibleTypes(string parentWithoutDot, string allCode)
        {
            var list = new List<Type>();
            if (parentWithoutDot == "")
                return list;
            list.AddRange(CustomVarTypeDefine.ContainsKey(parentWithoutDot) ? CustomVarTypeDefine[parentWithoutDot] : new List<Type>());
            if (parentWithoutDot.StartsWith("{"))
            {
                list.AddRange(GetQuickerVarTypes(parentWithoutDot));
                return list;
            }
            if (parentWithoutDot.EndsWith("]"))
            {
                var itemParent = Regex.Replace(parentWithoutDot, @"\[.*\]$", "");
                var enumType = GetParentPossibleTypes(itemParent, allCode);
                foreach (var type in enumType)
                {
                    list.AddRange(type.GetProperties().Where(x => x.Name == "Item").Select(x => x.PropertyType));
                }
                return list;
            }
            string typePattern = @"(?<type>(?<![<>,\w])[a-zA-Z][<>, \w]+)";
            string space = @" +";
            string delegratePattern = @" *=> *((?!\=\>).)*";
            var linqPattern = new Regex(String.Format(@"{1}{0}{1}(?!\w)(?:\[.*?\])?\.\w*$", delegratePattern, parentWithoutDot));
            if (linqPattern.IsMatch(allCode))
            {
                string varPattern = @"(?<![\w{])(?<var>[a-zA-Z{][\w}]*)";
                string linqFuncPattern = @"[A-Z][\w<>]+";
                var findEnumerablePattern = new Regex(String.Format(@"{0}\.{2}\( *{1}{3}{1}(?:\[.*?\])?\.\w*$", varPattern, parentWithoutDot, linqFuncPattern, delegratePattern));
                var match = findEnumerablePattern.Match(allCode);
                if (match.Success)
                {
                    var varName = match.Groups["var"].Value;
                    var enumType = GetParentPossibleTypes(varName, allCode.Substring(0, match.Groups["var"].Index + varName.Length + 1));
                    foreach (var type in enumType)
                    {
                        var interfaces = GetIEnumerableTs(type).Where(x => x.GenericTypeArguments.Count() == 1);
                        list.AddRange(interfaces.Select(x => x.GenericTypeArguments[0]));
                    }
                }
            }
            else
            {
                var pattern = new Regex(String.Format(@"{0}{2}{1}|{1}{2}={2}new{2}{0}", typePattern, parentWithoutDot, space));
                var match = pattern.Match(allCode);
                if (match.Success)
                {
                    list.AddRange(GetTypeWithString(match.Groups["type"].Value));
                }
            }

            return list;
        }

        private List<Type> GetQuickerVarTypes(string varName)
        {
            varName = varName.Trim('{', '}');
            try
            {
                var varInfo = quickerVarInfo.First(x => x["Key"].ToString() == varName);
                return GetTypeWithString(quickerVarMetaData[varInfo["Type"].ToString()]["type"].ToString());
            }
            catch
            {
                return new List<Type>();
            }
        }

        private List<Type> GetTypeWithString(string typestring)
        {
            List<Type> types = new List<Type>();
            if (typestring != "")
            {
                switch (typestring.Replace(" ", ""))
                {
                    case "List<string>":
                        types.Add(typeof(List<string>));
                        break;
                    case "Dictionary<string,object>":
                        types.Add(typeof(Dictionary<string, object>));
                        break;
                    case "int":
                        types.Add(typeof(int));
                        break;
                    case "double":
                        types.Add(typeof(double));
                        break;
                    case "bool":
                        types.Add(typeof(bool));
                        break;
                    case "string":
                        types.Add(typeof(string));
                        break;
                    case "String":
                        types.Add(typeof(string));
                        break;
                    case "DateTime":
                        types.Add(typeof(DateTime));
                        break;
                    case "Path":
                        types.Add(typeof(Path));
                        break;
                    case "File":
                        types.Add(typeof(File));
                        break;
                    case "Directory":
                        types.Add(typeof(Directory));
                        break;
                    case "Regex":
                        types.Add(typeof(Regex));
                        break;
                    case "Convert":
                        types.Add(typeof(Convert));
                        break;
                    case "JObject":
                        types.Add(typeof(JObject));
                        break;
                    case "JArray":
                        types.Add(typeof(JArray));
                        break;
                    case "Bitmap":
                        types.Add(typeof(Bitmap));
                        break;
                    case "Enumerable":
                        types.Add(typeof(Enumerable));
                        break;
                    case "TimeSpan":
                        types.Add(typeof(TimeSpan));
                        break;
                    case "JsonConvert":
                        types.Add(typeof(JsonConvert));
                        break;
                    default:
                        var itemsFromPredefindTypes = PredefindTypes.Where(x =>
                            GetGenericTypeName(x).Replace(" ", "") == typestring
                        );
                        if (itemsFromPredefindTypes.Count() > 0)
                        {
                            types.AddRange(itemsFromPredefindTypes);
                            break;
                        }
                        if (TypeGetter != null)
                        {
                            try
                            {
                                var temp = TypeGetter(typestring);
                                if (temp != null)
                                    types.Add(temp);
                            }
                            catch (Exception)
                            {
                            }

                        }

                        break;
                }
            }

            return types;
        }

        private List<CustomCompletionData> GetMethods(
            List<Type> types,
            BindingFlags flag = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static,
            bool isInstance = true)
        {
            var data = new List<CustomCompletionData>();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                Type[] interfaces = type.GetInterfaces();
                var methods = type.GetMethods(flag);
                //var extensionMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(t => t.IsDefined(typeof(ExtensionAttribute), false));
                foreach (var group in methods.Where(x => !x.Name.Contains("_")).GroupBy(x => x.Name))
                {
                    var method = group.First(x => true);
                    string description = String.Join("\r\n", group.Select(m =>
                    {
                        IEnumerable<ParameterInfo> paramss = null;
                        bool isExtension = m.IsDefined(typeof(ExtensionAttribute), false);
                        if (isExtension && isInstance)
                        {
                            paramss = m.GetParameters().Skip(1);
                        }
                        else
                            paramss = m.GetParameters();
                        return ((!isInstance) ? "static: " : "") + GetGenericTypeName(type)
                                + "."
                                + m.Name
                                + "("
                                + String.Join(", ", paramss.Select(y => GetGenericTypeName(y.ParameterType) + " " + y.Name))
                                + "): "
                                + m.ReturnType.Name;
                    }));
                    string genericPart = (method.IsGenericMethod ? "<>" : "");
                    var onedata = new CustomCompletionData()
                    {
                        name = method.Name + genericPart,
                        actualText = method.Name +
                                    ((method.IsGenericMethod && !method.ContainsGenericParameters) ? "<$1>" : "") +
                                    (method.GetParameters().Count() > 0 ? "($1" : "(") + ")",
                        priority = types.Count() - i,
                        description = description,
                        iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/method.png"
                    };
                    data.Add(onedata);

                }
                if (interfaces.Any(x => x.Name.StartsWith("IEnumerable")))
                    foreach (var group in typeof(Enumerable).GetMethods().Where(x => !x.Name.Contains("_")).GroupBy(x => x.Name))
                    {
                        var method = group.First(x => true);
                        var ts = GetIEnumerableTs(type).First(x => true);
                        var TSourse = ts.GetGenericArguments()[0].Name;
                        string description = String.Join("\r\n", group.Select(m =>
                        {
                            var paramss = m.GetParameters().Skip(1);
                            return GetGenericTypeName(ts)
                                + "."
                                + m.Name
                                + "("
                                + string.Join(", ", paramss.Select(x => GetGenericTypeName(x.ParameterType).Replace("TSource", TSourse) + " " + x.Name))
                                + "): "
                                + GetGenericTypeName(m.ReturnType);
                        }));
                        string genericPart = ((method.IsGenericMethod && !method.ContainsGenericParameters) ? "<>" : "");
                        var onedata = new CustomCompletionData()
                        {
                            name = method.Name + genericPart,
                            actualText = method.Name +
                                        ((method.IsGenericMethod && !method.ContainsGenericParameters) ? "<$1>" : "") +
                                        (method.GetParameters().Count() > 0 ? "($1" : "(") + ")",
                            priority = types.Count() - i,
                            description = description,
                            iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/method.png"
                        };
                        data.Add(onedata);

                    }
            }
            return data;
        }

        private static bool IsFunc(ParameterInfo x)
        {
            return (x.ParameterType.Name.StartsWith("Func") || x.ParameterType.Name.StartsWith("Action"));
        }

        private static IEnumerable<Type> GetIEnumerableTs(Type type)
        {
            return type.GetInterfaces().Where(x => x.Name.StartsWith("IEnumerable`"));
        }

        private static List<CustomCompletionData> GetPropertys(List<Type> types)
        {
            var data = new List<CustomCompletionData>();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                var props = type.GetProperties();

                foreach (var prop in props)
                {

                    if (!prop.Name.Contains("_"))
                    {
                        var onedata = new CustomCompletionData()
                        {
                            name = prop.Name,
                            actualText = prop.Name,
                            priority = types.Count() - i,
                            description = type.Name + "." + prop.Name + ": " + prop.PropertyType.Name,
                            iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/property.png"
                        };
                        data.Add(onedata);
                    }
                }
            }
            return data;
        }

        private static List<CustomCompletionData> GetFields(List<Type> types)
        {
            var data = new List<CustomCompletionData>();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                var fields = type.GetFields();

                foreach (var field in fields)
                {
                    if (!field.Name.Contains("_"))
                    {
                        var onedata = new CustomCompletionData()
                        {
                            name = field.Name,
                            actualText = field.Name,
                            priority = types.Count() - i,
                            description = type.Name + "." + field.Name + ": " + field.FieldType.Name,
                            iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/field.png"
                        };
                        data.Add(onedata);
                    }
                }
            }
            return data;
        }

        private string GetGenericTypeName(Type type)
        {
            string friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int iBacktick = friendlyName.IndexOf('`');
                if (iBacktick > 0)
                {
                    friendlyName = friendlyName.Remove(iBacktick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; ++i)
                {
                    string typeParamName = GetGenericTypeName(typeParameters[i]);
                    friendlyName += (i == 0 ? typeParamName : ", " + typeParamName);
                }
                friendlyName += ">";
            }

            return ValueTypeHandle(friendlyName);
        }

        public static string GetToken(TextArea sender)
        {
            // 获取光标位置前面的至多30个字符
            var po = sender.Caret.Position;

            var selection = new RectangleSelection(
                sender,
                new TextViewPosition(po.Line, po.Column - 30 >= 0 ? po.Column - 30 : 0),
                po);
            // 正则获取Parent
            var tokenMatch = Regex.Match(selection.GetText(), @"(?<=([^\w]|^))\w*$", RegexOptions.RightToLeft);
            if (tokenMatch.Success)
            {
                return tokenMatch.Value;
            }
            else
            {
                return "";
            }
        }

        public static string GetParent(TextArea sender)
        {
            // 获取光标位置前面的至多30个字符
            var currentCursorPosition = sender.Caret.Position;
            var selection = new RectangleSelection(sender, new TextViewPosition(currentCursorPosition.Line, currentCursorPosition.Column - 30 >= 0 ? currentCursorPosition.Column - 30 : 0), currentCursorPosition);
            // 正则获取Parent
            var parentMatch = Regex.Match(selection.GetText(), @"(?<=([^\w{}]|^))[^.]*?(?:\[.*?\])?\.(?=\w*$)", RegexOptions.RightToLeft);
            if (parentMatch.Success)
            {
                return parentMatch.Value;
            }
            else
            {
                return "";
            }
        }

        private static string GetCodeBeforeCaret(TextArea sender)
        {
            // 获取光标位置前面的至多30个字符
            var currentCursorPosition = sender.Caret.Offset;
            var selection = Selection.Create(sender, 0, currentCursorPosition);
            // 正则获取Parent
            return selection.GetText();
        }

        private static T GetPrivateFieid<T>(string name, object instance) where T : class
        {
            if (instance == null)
                return null;
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(instance) as T;
            return null;
        }

        private string ValueTypeHandle(string name)
        {
            return Regex.Replace(name, @"^(Int32|Double|String|Char)$", (m) =>
            {
                return m.Value
                        .Replace("Int32", "int")
                        .ToLower();
            });
        }

        private int MatchQuery(string text, string query)
        {
            if (query == "")
                return 1;
            if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 3;
            if (text.IndexOf(query, StringComparison.OrdinalIgnoreCase) != -1)
                return 2;
            if (query.All(x => text.IndexOf(x.ToString(), StringComparison.OrdinalIgnoreCase) != -1))
                return 1;

            if (CustomGetMatchQualityFunc != null)
                return CustomGetMatchQualityFunc(text, query);
            return -1;
        }

        #endregion Quicker
    }

    public delegate int CustomMatchQualityGetter(string text, string query);

    public delegate Type CustomVarTypeGetter(string varName);

    public delegate Task<IList<CustomCompletionData>> CustomCompletionDataGetterFromAPI(string allCode, int offset, char? triggerChar);

    public enum TriggerMode
    {
        Active,
        Passive
    }
    public static class TextAeraExt
    {
        public static string GetBehindText(this TextArea textArea, int length = 1)
        {
            string result;
            try
            {
                result = textArea.Document.GetText(textArea.Caret.Offset, length);
            }
            catch
            {
                result = "";
            }
            return result;
        }
        public static string GetAheadText(this TextArea textArea, int length = 1)
        {
            string result;
            try
            {
                result = textArea.Document.GetText(textArea.Caret.Offset - length, length);
            }
            catch
            {
                result = "";
            }
            return result;
        }
        public static void RemoveBehindText(this TextArea textArea, int length = 1)
        {
            textArea.Document.Remove(textArea.Caret.Offset, length);

        }
    }

    public class CustomCompletionData : ICompletionData
    {
        /// <summary>
        /// 替换选中补全项时的偏移量，默认为0，当非.触发时为token的长度
        /// </summary>
        internal int replaceOffset = -1;
        /// <summary>
        /// 替换完成后光标的偏移量，用来将光标定位到括号内等位置
        /// </summary>
        private int completeOffset = 0;
        /// <summary>
        /// 数据的文字介绍
        /// </summary>
        public string description = "";
        /// <summary>
        /// 数据在补全窗口中的显示的文字
        /// </summary>
        public string name;
        /// <summary>
        /// 实际用户选择后输入到屏幕上的文字
        /// </summary>
        public string actualText;
        /// <summary>
        /// 一般为空即可。数据的父类，比如 ToInt32() 的父类是「Convert.」
        /// </summary>
        public string parent = "";
        /// <summary>
        /// 当有多条数据时显示的顺序，值越大越靠上
        /// </summary>
        internal int priority = 100; 
        public string iconPath = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/Icon/custom.png";
        public System.Windows.Media.ImageSource iconImageSource = null;
        public System.Windows.Media.ImageSource Image
        {
            get
            {
                if (iconImageSource != null)
                    return iconImageSource;
                return new BitmapImage(new Uri(iconPath));
                //return GetImage(this.iconPath);
                //return null; 
            }
        }
        public string Text
        {
            get { return name; }
        }
        public object Content
        {
            get
            {
                return Text;
            }
        }
        public object Description
        {
            get
            {
                var control = new DescriptionControl();
                control.textBox2.Text = description;
                return control;
                //return description;
            }
        }
        public double Priority { get { return priority; } }
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            if(replaceOffset == -1)
            {
                var token = CodeCompletion.GetToken(textArea);
                this.replaceOffset = token.Length;
            }
            
            if (String.IsNullOrEmpty(this.actualText));
                this.actualText = this.Text;
            var replaceSegment = new SelectionSegment(completionSegment.Offset - this.replaceOffset, completionSegment.EndOffset);
            textArea.Document.Replace(replaceSegment, GetActualTextAndSetOffset(this.actualText));
            // 输入补全条目后进行光标位置偏移
            textArea.Caret.Offset = textArea.Caret.Offset - this.completeOffset;
        }
        private string GetActualTextAndSetOffset(string text)
        {
            var index = text.LastIndexOf("$1");
            if (index != -1)
            {
                int offset = text.Length - "$1".Length - index;
                this.completeOffset = offset;
                return text.Replace("$1", "");
            }
            else
            {
                return text;
            }

        }

    }
}
