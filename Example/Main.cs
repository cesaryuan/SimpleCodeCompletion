using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quicker.View.X.Controls.ParamEditors;
using Quicker.View.X.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Quicker.Domain.Actions.X.Storage;
using System.Collections.ObjectModel;
using Quicker.Utilities;
using System.Drawing;
using System.Windows.Media.Imaging;
using Z.Expressions;
using System.Runtime.CompilerServices;
using Quicker.View.X.Controls;
using SimpleCodeCompletion;

namespace TextWindowCodeCompletion
{
    class Main
    {
        public static void Init(TextEditor textEditor)
        {
            var dict = new Dictionary<string, List<Type>>() { { "test", new List<Type>() { typeof(Bitmap) } } };
            var AllCompletionData = new List<CustomCompletionData>();
            new CodeCompletion(textEditor,
                                CustomSnippets: AllCompletionData,
                                CustomGetMatchQualityFunc: null);
            textEditor.TextArea.Document.Text = @"List<string> ll;
ll.Select(x => x.Select(x => x))
Path
if
Dic
{test}";
        }
    }
}
