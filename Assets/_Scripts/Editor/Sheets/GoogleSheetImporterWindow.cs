#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoBattler.EditorTools.Sheets
{
    public sealed class GoogleSheetImporterWindow : EditorWindow
    {
        private const string InitialId="1pAdSSEjA9HgQyd1mMIRMfTsvwTgsuVyrAJ_nR6BhfvI";
        private string spreadsheet=InitialId, apiKey="", bearerToken="", report="Google Sheets에서 전체 표를 가져와 실행용 스냅샷에 적용.";
        private bool busy;
        private CancellationTokenSource operation;
        private Vector2 scroll;
        [DataContract] private sealed class ApiResponse { [DataMember] public ApiRange[] valueRanges; }
        [DataContract] private sealed class ApiRange { [DataMember] public string range; [DataMember] public List<List<object>> values; }
        public static List<CsvTable> ParseApiRows(string json)
        {
            var serializer=new DataContractJsonSerializer(typeof(ApiResponse));
            ApiResponse response;
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json))) response=(ApiResponse)serializer.ReadObject(stream);
            if(response.valueRanges==null) throw new FormatException("Sheets API 응답에 표 없음.");
            return response.valueRanges.Select(range=>new CsvTable(range.range.Split('!')[0].Trim('\''),
                (range.values ?? new List<List<object>>()).Select(row=>row.Select(value=>Convert.ToString(value,CultureInfo.InvariantCulture)).ToList()).ToList())).ToList();
        }
        [MenuItem("PixelTactics/Data/Google Sheets Import")]
        private static void Open()=>GetWindow<GoogleSheetImporterWindow>("Sheets Import");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("전체 표 가져오기 → 오류 확인 → JSON 교체",EditorStyles.boldLabel);
            spreadsheet=EditorGUILayout.TextField("시트 URL 또는 ID",spreadsheet);
            apiKey=EditorGUILayout.PasswordField("Sheets API Key",apiKey);
            bearerToken=EditorGUILayout.PasswordField("OAuth Access Token",bearerToken);
            EditorGUILayout.HelpBox("인증 값은 현재 창 메모리에만 보관. 비워두면 공개 시트 CSV 사용. 비공개 시트는 읽기 권한이 있는 OAuth 토큰 필요. 빈 시트는 적용하지 않음.",MessageType.Info);
            using(new EditorGUI.DisabledScope(busy))
            {
                if(GUILayout.Button("Google Sheets에서 가져오기")) ImportAsync().Forget();
            }
            if(busy && GUILayout.Button("취소")) operation.Cancel();
            scroll=EditorGUILayout.BeginScrollView(scroll); EditorGUILayout.TextArea(report,GUILayout.ExpandHeight(true)); EditorGUILayout.EndScrollView();
        }
        private static string ExtractId(string input)
        {
            int start=input.IndexOf("/d/",StringComparison.Ordinal);
            if(start>=0) { string tail=input.Substring(start+3); return tail.Split('/','?','#')[0]; }
            return input.Trim();
        }
        private async UniTask ImportAsync()
        {
            busy=true; operation=new CancellationTokenSource(); report="가져오는 중...";
            try
            {
                var tables=new Dictionary<string,CsvTable>(); string id=ExtractId(spreadsheet);
                if(string.IsNullOrWhiteSpace(id)) throw new FormatException("스프레드시트 ID 필요.");
                await LoadTables(id,SnapshotImporter.BaseTabs,tables);
                int schema=tables["Manifest"].Int(1,"schema_version");
                var extraTabs=SnapshotImporter.TabsForSchema(schema).Except(SnapshotImporter.BaseTabs).ToArray();
                if(extraTabs.Length>0) await LoadTables(id,extraTabs,tables);
                operation.Token.ThrowIfCancellationRequested(); Apply(tables);
            }
            catch(OperationCanceledException) { report="가져오기 취소. 기존 스냅샷 유지."; }
            catch(Exception exception) { report="적용 안 됨. 기존 스냅샷 유지.\n"+exception.Message; }
            finally { busy=false; operation.Dispose(); operation=null; Repaint(); }
        }
        private async UniTask LoadTables(string id,string[] tabs,Dictionary<string,CsvTable> tables)
        {
                if(!string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(bearerToken))
                {
                    string url="https://sheets.googleapis.com/v4/spreadsheets/"+Uri.EscapeDataString(id)+"/values:batchGet?valueRenderOption=UNFORMATTED_VALUE&majorDimension=ROWS";
                    foreach(string tab in tabs) url+="&ranges="+Uri.EscapeDataString(tab+"!A:Z");
                    if(!string.IsNullOrWhiteSpace(apiKey)) url+="&key="+Uri.EscapeDataString(apiKey);
                    string json=await Download(url,operation.Token);
                    var responseTables=ParseApiRows(json);
                    if(responseTables.Count!=tabs.Length) throw new FormatException("Sheets API 응답의 표 수 오류.");
                    foreach(var table in responseTables) tables.Add(table.Name,table);
                }
                else
                {
                    foreach(string tab in tabs)
                    {
                        report="가져오는 중: "+tab; Repaint();
                        string url="https://docs.google.com/spreadsheets/d/"+Uri.EscapeDataString(id)+"/gviz/tq?tqx=out:csv&sheet="+Uri.EscapeDataString(tab);
                        string csv=await Download(url,operation.Token);
                        if(csv.TrimStart().StartsWith("<")) throw new FormatException(tab+": 공개 CSV 응답 없음. 공유 권한 또는 OAuth 인증 확인.");
                        tables.Add(tab,new CsvTable(tab,csv));
                    }
                }
        }
        private async UniTask<string> Download(string url,CancellationToken token)
        {
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;
                if(!string.IsNullOrWhiteSpace(bearerToken)) request.SetRequestHeader("Authorization","Bearer "+bearerToken);
                await request.SendWebRequest().ToUniTask(cancellationToken:token);
                if(request.result!=UnityWebRequest.Result.Success) throw new IOException($"HTTP {request.responseCode}: 가져오기 실패.");
                return request.downloadHandler.text;
            }
        }
        private void Apply(Dictionary<string,CsvTable> tables)
        {
            var snapshot=SnapshotImporter.Build(tables); SnapshotImporter.Apply(snapshot);
            report=$"적용 완료: {snapshot.dataVersion}\n유닛 {snapshot.units.Length}종 / 스킬 {snapshot.skills.Length}종 / 시너지 {snapshot.traits.Length}단계 / 강화 {snapshot.augments.Length}종 / 라운드 {snapshot.rounds.Length}개\n{SnapshotImporter.OutputPath}\n로컬 Addressables 번들을 다시 빌드한 뒤 배포.";
        }
        private void OnDisable() { operation?.Cancel(); apiKey=""; bearerToken=""; }
    }
}
#endif
