using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.Drawing;
using Z.Expressions;
using SimpleCodeCompletion;

namespace TextWindowCodeCompletion
{
    class Main
    {
        static EvalContext evalContext;
        public static void Init(TextEditor textEditor)
        {
            var dict = new Dictionary<string, List<Type>>() { { "test", new List<Type>() { typeof(Bitmap) } } };
            var AllCompletionData = new List<CustomCompletionData>();
            
            new CodeCompletion(textEditor,
                                CustomSnippets: AllCompletionData,
                                CustomGetMatchQualityFunc: null,
                                TypeGetter: new Func<string, Type>(gettype));
            textEditor.TextArea.Document.Text = @"List<string> ll;
ll.Select(x => x.Select(x => x))
ll[0]
Path
if
Dic
{test}
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
