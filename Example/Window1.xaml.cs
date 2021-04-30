// Copyright (c) 2009 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

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
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window123 : Window
    {

        public Window123()
        {
            InitializeComponent();
            Main.Init(this.textEditor11);
        }



        #region Quicker

        public static void Exec(Quicker.Public.IStepContext context)
        {
            EvalContext evalContext = null;
            Dictionary<string, List<Type>> varTypeDict = new Dictionary<string, List<Type>>();
            JObject QuickcerVarInfo = JObject.Parse(@"{""0"": {""name"": ""Text"",""type"": ""string""},""1"": {""name"": ""Number"",""type"": ""double""},""2"": {""name"": ""Boolean"",""type"": ""bool""},""3"": {""name"": ""Image"",""type"": ""Bitmap""},""4"": {""name"": ""List"",""type"": ""List<string>""},""6"": {""name"": ""DateTime"",""type"": ""DateTime""},""7"": ""Keyboard"",""8"": ""Mouse"",""9"": ""Enum"",""10"": {""name"": ""Dict"",""type"": ""Dictionary<string, object>""},""11"": ""Form"",""12"": {""name"": ""Integer"",""type"": ""int""},""98"": {""name"": ""Object"",""type"": ""Object""},""99"": {""name"": ""Object"",""type"": ""Object""},""100"": ""NA"",""101"": ""CreateVar""}");

            var data = (string)context.GetVarValue("代码片段");
            Func<string, Type> typeGetter = (s) =>
            {
                try
                {
                    if (evalContext == null)
                    {
                        evalContext = new EvalContext();
                        evalContext.UseLocalCache = true;
                    }

                    //AppHelper.ShowInformation(typestring);
                    var typ = evalContext.Execute<Type>("typeof(" + s + ")");
                    return typ;
                }
                catch (Exception e)
                {
                    return null;
                }
            };
            var AllCompletionData = JsonConvert.DeserializeObject<List<CustomCompletionData>>(data);

            Window win;
            win = WinOp.GetWindow<Quicker.View.CodeEditorWindow>();
            if (win == null)
            {
                win = WinOp.GetWindow<Quicker.View.X.ActionStepEditorWindow>();
                if (win == null)
                    win = WinOp.GetWindow<Quicker.View.TextWindow>();
                if (win == null)
                    throw new Exception("您使用的地方不对，请在Quicker动作步骤编辑窗口或者代码编辑器窗口使用");
            }
            var type = win.GetType();

            if (win.Tag != null)
                return;

            #region 注入代码
            try
            {
                varTypeDict.Add("_eval", new List<Type>() { typeof(EvalContext) });
            }
            catch { }

            if (type == typeof(Quicker.View.CodeEditorWindow))
            {

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var textEditor = GetPrivateFieid<TextEditor>("textEditor", win);
                    var _variables = GetPrivateFieid<ICollection<ActionVariable>>("_variables", win);
                    CompletionWindow originCompletionWindow = GetPrivateFieid<CompletionWindow>("completionWindow", win);
                    new CodeCompletion(
                        textEditor,
                        completionWindow: originCompletionWindow,
                        CustomSnippets: AllCompletionData,
                        CustomGetMatchQualityFunc: AvalonEditExt.GetMatchQuality,
                        QuickerVarInfo: JArray.FromObject(_variables),
                        CustomVarTypeDefine: varTypeDict,
                        TypeGetter: typeGetter);
                    AppHelper.ShowSuccess("启动成功");
                    win.Tag = true;

                });
            }
            else if (type == typeof(Quicker.View.X.ActionStepEditorWindow))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var _variables = GetPrivateFieid<ObservableCollection<ActionVariable>>("_variables", win);
                    if (_variables == null)
                        _variables = (win as Quicker.View.X.ActionStepEditorWindow).Variables;
                    var _inputParamEditors = GetPrivateFieid<List<InputParamEditor2>>("_inputParamEditors", win);
                    if (_inputParamEditors == null)
                        _inputParamEditors = GetPrivateFieid<Dictionary<string, StepInputFieldControl>>("_inputFieldControls", win).Select(x =>
                        {
                            return GetPrivateFieid<InputParamEditor2>("_editor", x.Value);
                        }).ToList();

                    if (_inputParamEditors != null)
                    {

                        foreach (InputParamEditor2 inp in _inputParamEditors)
                        {


                            VarAndValueParamEditor2 paramEditor = GetPrivateFieid<ContentControl>("Wrapper", inp).Content as VarAndValueParamEditor2;
                            if (paramEditor != null)
                            {

                                TextEditor textEditor = GetPrivateFieid<TextEditor>("TxtEditor", paramEditor);
                                CompletionWindow originCompletionWindow = GetPrivateFieid<CompletionWindow>("completionWindow", paramEditor);
                                if (textEditor != null)
                                {
                                    new CodeCompletion(
                                        textEditor,
                                        completionWindow: GetPrivateFieid<CompletionWindow>("completionWindow", paramEditor),
                                        CustomSnippets: AllCompletionData,
                                        CustomGetMatchQualityFunc: AvalonEditExt.GetMatchQuality,
                                        QuickerVarInfo: JArray.FromObject(_variables),
                                        CustomVarTypeDefine: varTypeDict,
                                        TypeGetter: typeGetter);

                                }


                            }
                        }
                        win.Tag = true;
                        AppHelper.ShowSuccess("启动成功");
                    }
                });
            }
            else if (type == typeof(Quicker.View.TextWindow))
            {

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var textEditor = GetPrivateFieid<TextEditor>("TheText", win);
                    new CodeCompletion(
                        textEditor,
                        completionWindow: null,
                        CustomSnippets: AllCompletionData,
                        CustomGetMatchQualityFunc: AvalonEditExt.GetMatchQuality,
                        TypeGetter: typeGetter);
                });
                win.Tag = true;
                AppHelper.ShowSuccess("启动成功");
            }
            #endregion

        }

        public static T GetPrivateFieid<T>(string name, object instance) where T : class
        {
            if (instance == null)
                return null;
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(instance) as T;
            return null;
        }


        #endregion Quicker
    }

    static class WinOp
    {
        [DllImport("User32.dll")]
        public static extern IntPtr GetForegroundWindow();     //获取活动窗口句柄
        public static IntPtr GetHandle(Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }
        public static WType GetWindow<WType>() where WType : class
        {
            IntPtr handle = GetForegroundWindow();
            HwndSource hwndSource = HwndSource.FromHwnd(handle);
            WType winGet = hwndSource.RootVisual as WType;
            return winGet;
        }
        public static Window GetWindowByHandle(int intHandle)
        {
            IntPtr handle = new IntPtr(intHandle);
            HwndSource hwndSource = HwndSource.FromHwnd(handle);
            Window winGet = hwndSource.RootVisual as Window;
            return winGet;
        }
        public static WType GetWindowByHandle<WType>(int intHandle) where WType : class
        {
            IntPtr handle = new IntPtr(intHandle);
            HwndSource hwndSource = HwndSource.FromHwnd(handle);
            WType winGet = hwndSource.RootVisual as WType;
            return winGet;
        }
    }

}