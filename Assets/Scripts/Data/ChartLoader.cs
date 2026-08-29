using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// 빌드된 게임 옆의 StreamingAssets/Charts 폴더에서 JSON 채보를 읽어온다.
// 여기 있는 파일만 바꾸면 재빌드 없이 채보/오디오/커버를 교체할 수 있다.
public static class ChartLoader
{
    public static string ChartsFolder => Path.Combine(Application.streamingAssetsPath, "Charts");

    public static IEnumerator LoadAllCharts(Action<List<ChartData>> onComplete)
    {
        var result = new List<ChartData>();

        if (!Directory.Exists(ChartsFolder))
        {
            Debug.LogWarning($"[ChartLoader] Charts 폴더가 없습니다: {ChartsFolder}");
            onComplete?.Invoke(result);
            yield break;
        }

        foreach (string jsonPath in Directory.GetFiles(ChartsFolder, "*.json", SearchOption.AllDirectories))
        {
            ChartData chart = null;
            try
            {
                string json = File.ReadAllText(jsonPath);
                chart = JsonUtility.FromJson<ChartData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChartLoader] {jsonPath} 파싱 실패: {ex.Message}");
            }

            if (chart == null) continue;

            foreach (var note in chart.notes)
            {
                if (!Enum.TryParse(note.type, out note.typeEnum))
                {
                    Debug.LogWarning($"[ChartLoader] '{chart.songName}': 알 수 없는 노트 타입 '{note.type}' → Hit으로 대체");
                    note.typeEnum = NoteType.Hit;
                }
            }

            string folder = Path.GetDirectoryName(jsonPath);

            if (!string.IsNullOrEmpty(chart.audioFile))
            {
                string audioPath = Path.Combine(folder, chart.audioFile);
                if (File.Exists(audioPath))
                {
                    AudioType audioType;
                    switch (Path.GetExtension(audioPath).ToLowerInvariant())
                    {
                        case ".wav": audioType = AudioType.WAV; break;
                        case ".mp3": audioType = AudioType.MPEG; break;
                        default: audioType = AudioType.OGGVORBIS; break;
                    }

                    using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, audioType))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success)
                            chart.audioClip = DownloadHandlerAudioClip.GetContent(req);
                        else
                            Debug.LogError($"[ChartLoader] 오디오 로드 실패: {audioPath} ({req.error})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ChartLoader] 오디오 파일을 찾을 수 없습니다: {audioPath}");
                }
            }

            if (!string.IsNullOrEmpty(chart.coverFile))
            {
                string coverPath = Path.Combine(folder, chart.coverFile);
                if (File.Exists(coverPath))
                {
                    byte[] bytes = File.ReadAllBytes(coverPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                        chart.coverSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                else
                {
                    Debug.LogWarning($"[ChartLoader] 커버 이미지를 찾을 수 없습니다: {coverPath}");
                }
            }

            result.Add(chart);
        }

        onComplete?.Invoke(result);
    }
}
