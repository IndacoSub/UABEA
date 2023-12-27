using System.Collections.Generic;
using System.Text;

namespace UAFGJ
{
	partial class Program
	{
		private bool StartsWithSpace(string str, string value)
		{
			return str.StartsWith(value + " ");
		}

		private string UnescapeDumpString(string str)
		{
			StringBuilder sb = new StringBuilder(str.Length);
			bool escaping = false;
			foreach (char c in str)
			{
				if (!escaping && c == '\\')
				{
					escaping = true;
					continue;
				}

				if (escaping)
				{
					if (c == '\\')
						sb.Append('\\');
					else if (c == 'r')
						sb.Append('\r');
					else if (c == 'n')
						sb.Append('\n');
					else
						sb.Append(c);

					escaping = false;
				}
				else
				{
					sb.Append(c);
				}
			}

			return sb.ToString();
		}

		private void ImportTextAssetLoop()
		{
			Stack<bool> alignStack = new Stack<bool>();
			while (true)
			{
				string? line = sr.ReadLine();
				if (line == null)
					return;

				int thisDepth = 0;
				while (line[thisDepth] == ' ')
					thisDepth++;

				if (line[thisDepth] == '[') //array index, ignore
					continue;

				if (thisDepth < alignStack.Count)
				{
					while (thisDepth < alignStack.Count)
					{
						if (alignStack.Pop())
							aw.Align();
					}
				}

				bool align = line.Substring(thisDepth, 1) == "1";
				int typeName = thisDepth + 2;
				int eqSign = line.IndexOf('=');
				string valueStr = line.Substring(eqSign + 1).Trim();

				if (eqSign != -1)
				{
					string check = line.Substring(typeName);
					//this list may be incomplete
					if (StartsWithSpace(check, "bool"))
					{
						aw.Write(bool.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "UInt8"))
					{
						aw.Write(byte.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "SInt8"))
					{
						aw.Write(sbyte.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "UInt16"))
					{
						aw.Write(ushort.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "SInt16"))
					{
						aw.Write(short.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "unsigned int"))
					{
						aw.Write(uint.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "int"))
					{
						aw.Write(int.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "UInt64"))
					{
						aw.Write(ulong.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "SInt64"))
					{
						aw.Write(long.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "float"))
					{
						aw.Write(float.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "double"))
					{
						aw.Write(double.Parse(valueStr));
					}
					else if (StartsWithSpace(check, "string"))
					{
						int firstQuote = valueStr.IndexOf('"');
						int lastQuote = valueStr.LastIndexOf('"');
						string valueStrFix = valueStr.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
						valueStrFix = UnescapeDumpString(valueStrFix);
						aw.WriteCountStringInt32(valueStrFix);
					}

					if (align)
					{
						aw.Align();
					}
				}
				else
				{
					alignStack.Push(align);
				}
			}
		}
	}
}