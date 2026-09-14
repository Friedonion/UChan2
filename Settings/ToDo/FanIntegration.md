# 부채 모델 게임 로직 연동 ToDo

## 배경
FanTest 씬에서 OpenFan.fbx 모델 + FanAnim.anim 애니메이션이 완성된 상태.
이를 BasicScene의 실제 게임 로직(FanSystem.cs, Note.cs)에 연동하는 작업.

**부채 상태 규칙 (변경 없음)**
- 트리거 홀드 중 → 부채 펼침 (IsOpened = true)
- 트리거 뗌 → 부채 접힘 (IsOpened = false)
- `Hit` 노트 → 접힌 상태에서 타격
- `Slashing / Fanning` 노트 → 펼친 상태에서 타격

---

## 작업 순서

### 1. Animator Controller 설정 (Unity 에디터)
- [ ] `Assets/Models/Fan/OpenFan.controller` 열기
- [ ] Default State에 `FanAnim` 클립 연결
- [ ] State Machine 파라미터 불필요 (코드에서 normalizedTime으로 직접 제어)

---

### 2. FanSystem.cs 수정 (코드)

#### 2-1. Animator 필드 추가
```csharp
[Header("Fan Model")]
public Animator fanAnimator;  // OpenFan 오브젝트의 Animator
```

#### 2-2. 큐브 렌더러 숨김
`CreatePrototypeFan()` 안에서 큐브 생성 후:
```csharp
rib.GetComponent<MeshRenderer>().enabled = false;
```
- 콜라이더는 유지 (판정에 필요)
- 시각은 새 OpenFan 모델이 담당

#### 2-3. Animator 구동
`AnimateFan()` 안에서 `currentOpenAmount` 계산 후 추가:
```csharp
if (fanAnimator != null)
    fanAnimator.Play("FanAnim", 0, 1f - currentOpenAmount);
    // currentOpenAmount 0(접힘) → normalizedTime 1 (clip 끝 = 접힘)
    // currentOpenAmount 1(펼침) → normalizedTime 0 (clip 시작 = 펼침)
```

---

### 3. BasicScene 씬 작업 (Unity 에디터)
- [ ] BasicScene 열기
- [ ] FanSystem 오브젝트 찾기
- [ ] `Assets/Models/Fan/OpenFan.fbx` 프리팹을 FanSystem의 **자식**으로 드래그
- [ ] OpenFan의 위치·회전·크기를 기존 큐브 부채와 겹치도록 맞춤
- [ ] FanSystem Inspector의 `Fan Animator` 필드에 OpenFan의 Animator 컴포넌트 드래그

---

### 4. 확인 (변경 불필요)
- `Note.cs`의 판정 로직은 `fan.IsOpened` 기반으로 이미 완성
- 별도 수정 없이 새 모델과 호환됨

---

## 파일 경로 참조
| 파일 | 경로 |
|------|------|
| 부채 모델 | `Assets/Models/Fan/OpenFan.fbx` |
| 애니메이션 클립 | `Assets/Models/Fan/FanAnim.anim` |
| Animator Controller | `Assets/Models/Fan/OpenFan.controller` |
| 게임 로직 | `Assets/Scripts/FanSystem.cs` |
| 노트 판정 | `Assets/Scripts/Note.cs` |
| 게임 씬 | `Assets/Scenes/BasicScene.unity` |
