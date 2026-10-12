#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoBattler.EditorTools.Sheets
{
    /// <summary>따옴표, 쉼표, 줄바꿈을 보존하는 CSV 입력 어댑터.</summary>
    public sealed class CsvTable
    {
        public string Name { get; }
        public List<List<string>> Rows { get; }
        private readonly Dictionary<string,int> columns=new Dictionary<string,int>();
        public CsvTable(string name,string text):this(name,Parse(text)) { }
        public CsvTable(string name,List<List<string>> rows)
        {
            Name=name; Rows=rows;
            if(rows.Count==0) throw new FormatException($"{name}: 빈 표.");
            for(int i=0;i<rows[0].Count;i++)
            {
                string column=rows[0][i].Trim().TrimStart('\ufeff');
                if(columns.ContainsKey(column)) throw new FormatException($"{name} 1행 {column}: 중복 열.");
                columns.Add(column,i);
            }
        }
        public bool IsEmpty(int row)=>Rows[row].TrueForAll(string.IsNullOrWhiteSpace);
        public string Get(int row,string column)
        {
            if(!columns.TryGetValue(column,out int index)) throw Error(row,column,"필수 열 없음.");
            return index<Rows[row].Count?Rows[row][index].Trim():"";
        }
        public int Int(int row,string column,bool optional=false)
        {
            string value=Get(row,column); if(optional && value.Length==0) return 0;
            if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result)) throw Error(row,column,"정수 필요: "+value);
            return result;
        }
        public float Float(int row,string column)
        {
            if(!float.TryParse(Get(row,column),NumberStyles.Float,CultureInfo.InvariantCulture,out float result) || float.IsNaN(result) || float.IsInfinity(result))
                throw Error(row,column,"유효한 숫자 필요.");
            return result;
        }
        public bool Bool(int row,string column)
        {
            string value=Get(row,column);
            if(value=="1" || value.Equals("true",StringComparison.OrdinalIgnoreCase)) return true;
            if(value=="0" || value.Equals("false",StringComparison.OrdinalIgnoreCase)) return false;
            throw Error(row,column,"true/false 또는 1/0 필요.");
        }
        public FormatException Error(int row,string column,string cause)
        {
            string id=row>=0 && row<Rows.Count && Rows[row].Count>0?Rows[row][0]:"없음";
            return new FormatException($"{Name} {row+1}행 {column} (ID {id}): {cause}");
        }
        private static List<List<string>> Parse(string text)
        {
            var rows=new List<List<string>>(); var row=new List<string>(); var field=new StringBuilder();
            bool quoted=false,closed=false;
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];
                if(quoted)
                {
                    if(c=='"') { if(i+1<text.Length && text[i+1]=='"') { field.Append('"'); i++; } else { quoted=false; closed=true; } }
                    else field.Append(c);
                    continue;
                }
                if(c=='"')
                {
                    if(field.Length!=0 || closed) throw new FormatException("CSV: 잘못된 따옴표.");
                    quoted=true;
                }
                else if(c==',') { row.Add(field.ToString()); field.Clear(); closed=false; }
                else if(c=='\r' || c=='\n')
                {
                    if(c=='\r' && i+1<text.Length && text[i+1]=='\n') i++;
                    row.Add(field.ToString()); rows.Add(row); row=new List<string>(); field.Clear(); closed=false;
                }
                else { if(closed && !char.IsWhiteSpace(c)) throw new FormatException("CSV: 닫는 따옴표 뒤 문자."); if(!closed) field.Append(c); }
            }
            if(quoted) throw new FormatException("CSV: 닫히지 않은 따옴표.");
            if(field.Length>0 || row.Count>0 || closed) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
#endif
