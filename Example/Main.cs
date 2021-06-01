using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Drawing;
using Z.Expressions;
using SimpleCodeCompletion;
using Newtonsoft.Json.Linq;

namespace TextWindowCodeCompletion
{
    class Main
    {
        static EvalContext evalContext;
        public static void Init(TextEditor textEditor)
        {
            var dict = new Dictionary<string, List<Type>>() { { "[test]", new List<Type>() { typeof(Bitmap) } } };
            var AllCompletionData = new List<CustomCompletionData>();
            var aa = new JArray()
            {
                JObject.FromObject(new {
                    Key="[test]",
                    Type=0,
                    Desc="",
                    DefaultValue=""
                })
            };
            new CodeCompletion(textEditor,
                                QuickerVarInfo: aa
                              );
            textEditor.TextArea.Document.Text = @"List<string> abcd;
ll.Select(x => x.Select(x => x))
ll[0]
Path
if
Dic
{test}
foreach (var item in {test})
{
	item
}
foreach (var item in new List<Bitmap>())
{
	item.
}
";
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
    }
}
