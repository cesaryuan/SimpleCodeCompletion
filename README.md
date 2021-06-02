# SimpleCodeCompletion
为 ICSharpCode.AvalonEdit.TextEditor 实现一个简单的C#自动补全

## 使用方法

```csharp
new CodeCompletion(textEditor, quickerVarInfo);
```

## 可配置的静态属性

`CustomCompletionDataFromAPI` 定义从网络API获取补全数据的函数，该函数只会在简易补全不生效和主动触发补全的使用

`PredefindTypes` 自动补全时支持的类型

`CustomSnippets` 自定义Snippet，有内置几个常用的，比如if和for结构

`CustomGetMatchQualityFunc` 补全项目匹配函数。输入：string text, string query, 输出：匹配度

`PairChars` 对符，默认有括号、单引号、双引号
