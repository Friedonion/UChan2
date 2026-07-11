# 트러블슈팅 노트

## VR 레이/클릭이 특정 씬에서만 안 될 때 (2026-07-06)

**증상:** 곡 선택(MusicSelectUI)에서는 레이로 버튼 클릭이 잘 되는데, BasicScene의 Pause 메뉴 버튼만 레이는 보이는데 클릭이(호버조차) 전혀 안 됨.

**확인된 원인 3가지 (BasicScene 고유, 전부 동시에 걸려있었음):**

1. **위치 어긋남** — `PauseManager.cs`의 `PositionPanelInFrontOfCamera()`가 자식 오브젝트(`pausePanel`)의 Transform만 카메라 앞으로 옮기고, 실제 `Canvas` + `TrackedDeviceGraphicRaycaster`가 붙은 부모(`PauseCanvas`)는 원래 자리에 그대로 뒀음. 레이캐스트는 Canvas의 Transform 기준으로 평면을 계산하므로, 시각적 버튼 위치와 실제 클릭 판정 위치가 어긋남.
   → **수정:** 부모(`pausePanel.transform.parent`)의 Transform을 옮기도록 변경.

2. **blockingMask** — `TrackedDeviceGraphicRaycaster.blockingMask` 기본값이 `-1`(Everything). World Space UI는 카메라와 캔버스 사이에 아무 물리 콜라이더나 있으면 "가려짐" 처리돼서 레이가 무효화됨. BasicScene은 바닥/맵 같은 3D 지형이 있어서 걸렸고, MusicSelectUI는 빈 UI 씬이라 문제 없었음.
   → **수정:** PauseCanvas의 `blockingMask`를 `0`(Nothing)으로.

3. **숨겨진 투명 Image** — `UI/MainHUD` 오브젝트 자체에 `raycastTarget=true`인 완전 투명(alpha=0) 1920x1080 크기 Image가 있었음. HP/콤보 HUD를 Pause 패널보다 앞에 보이게 하려고 `UI` 캔버스의 `sortingOrder`를 올렸더니, 이 투명 판이 모든 클릭을 먼저 가로챔.
   → **수정:** 해당 Image의 `raycastTarget`을 `false`로.

**디버깅 팁:** 컴포넌트 설정을 정적으로(에디터에서 값만 비교) 비교하는 것보다, `EditorApplication.isPlaying = true`로 실제 Play 모드에 들어가서 `NearFarInteractor.TryGetCurrentUIRaycastResult()` 같은 걸 실시간으로 찍어보는 게 훨씬 빨리 원인을 찾을 수 있었음. `selectionRegion`은 "현재 무엇을 조준 중인가"가 아니라 "현재 선택(그랩) 중인 대상이 Near/Far 어디서 왔는가"라서, 평상시(아무것도 선택 안 함)엔 항상 `None`으로 나옴 — 호버 실패의 증거로 오해하지 말 것.

## 커스텀 한글 TMP 폰트 만들 때 주의

`TMP_FontAsset.CreateFontAsset(...)`로 폰트 에셋을 코드로 생성한 뒤 `AssetDatabase.CreateAsset()`만 하면, 재시작/재로드 시 `UnassignedReferenceException: m_AtlasTextures has not been assigned` 예외가 남. 반드시 아래처럼 아틀라스 텍스처와 머티리얼을 **서브에셋으로 같이 저장**해야 함:

```csharp
AssetDatabase.CreateAsset(fontAsset, outPath);
AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
AssetDatabase.SaveAssets();
```
