namespace AutoBattler.Presentation.Battle
{
    /// <summary>공용 골드 스프라이트와 금액 표시. TMP에 연결한 통화 스프라이트의 첫 글리프 사용.</summary>
    public static class CurrencyText
    {
        public static string Amount(int value,string prefix="")=>$"<sprite index=0> {prefix}{value}";
        public static string Cost(int value,int available)=>available<value
            ?$"<sprite index=0> <color=#FF6565>{value}</color>":Amount(value);
    }
}
