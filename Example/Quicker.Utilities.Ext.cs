using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextWindowCodeCompletion
{
	public static class AvalonEditExt
	{

		// Token: 0x060026FA RID: 9978 RVA: 0x000A1C00 File Offset: 0x0009FE00
		public static int GetMatchQuality(string itemText, string query)
		{
			if (itemText == null)
			{
				throw new ArgumentNullException("itemText", "ICompletionData.Text returned null");
			}
			if (itemText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
			{
				return 1;
			}
			return -1;
		}
	}
	public static class DictExt
	{

		// Token: 0x060026FA RID: 9978 RVA: 0x000A1C00 File Offset: 0x0009FE00
		public static int GetMatchQuality(string itemText, string query)
		{
			if (itemText == null)
			{
				throw new ArgumentNullException("itemText", "ICompletionData.Text returned null");
			}
			if (itemText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
			{
				return 1;
			}
			return -1;
		}
	}
	public static class StringExt
	{

		// Token: 0x060026FA RID: 9978 RVA: 0x000A1C00 File Offset: 0x0009FE00
		public static int GetMatchQuality(string itemText, string query)
		{
			if (itemText == null)
			{
				throw new ArgumentNullException("itemText", "ICompletionData.Text returned null");
			}
			if (itemText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
			{
				return 1;
			}
			return -1;
		}
	}
}
