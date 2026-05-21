# BattleSim.Core Smoke Fixtures

세 가지 canonical 시나리오로 Unity 클라이언트 연동을 빠르게 검증한다.

## 시나리오 요약

| 시나리오 | 커맨드 | 종료 tick | 결과 |
|---------|--------|-----------|------|
| side_a_victory | SideA SpawnDroneSquad (tick 0) | 1 | WinnerSide=SideA / SideBBaseDestroyed |
| side_b_victory | SideB SpawnDroneSquad (tick 0) | 1 | WinnerSide=SideB / SideABaseDestroyed |
| timeout_side_b_tiebreak | 없음 | 3 | WinnerSide=SideB / TimeOut (tie → tieWinnerSide) |

**side_a_victory** — SideA 드론 speed(2000) > lane 길이(1000) → tick 1에 SideB base(HP=1) 파괴.  
**side_b_victory** — SideB 드론 speed(2000) > lane 길이(1000) → tick 1에 SideA base(HP=1) 파괴.  
**timeout_side_b_tiebreak** — 커맨드 없음, maxBattleTick=3 도달, HP 동률 → TimeOutTieWinnerSide=SideB 승리.

PvE 해석: SideA = 로컬 플레이어, SideB = AI 컨트롤러.

---

## 클라이언트 tick loop 예시 (C#)

```csharp
// 1. config + initial state 구성 (fixtures의 JSON 값 참조)
BattleConfigSnapshot config = BuildConfig();   // BattleSideConfig sideA, sideB 포함
BattleInitialState initial  = BuildInitialState(); // BattleSideInitialState sideA, sideB 포함

// 2. 시뮬레이터 생성
var sim = new BattleSimulator(config, initial);

// 3. tick loop
while (!sim.IsTerminated)
{
    // 현재 tick에 해당하는 커맨드 제출 (SideA 플레이어 입력)
    foreach (BattleCommand cmd in GetLocalCommandsForTick(sim.CurrentTick))
        sim.SubmitCommand(cmd);

    // SideB AI 커맨드 제출 (외부 AI 컨트롤러가 생성)
    foreach (BattleCommand cmd in GetAICommandsForTick(sim.CurrentTick))
        sim.SubmitCommand(cmd);

    sim.AdvanceTick();

    // 상태 읽기 → Unity 비주얼 업데이트
    BattleState state = sim.GetState();
    UpdateVisuals(state);            // Sides[0]=SideA, Sides[1]=SideB
}

// 4. 결과 처리
BattleResult result = sim.GetResult();
// result.WinnerSide         → BattleSide.SideA | BattleSide.SideB
// result.EndReason          → SideBBaseDestroyed | SideABaseDestroyed | TimeOut
// result.ClearTimeTick      → 종료 tick
// result.SideABaseHpRatio   → SideA 기지 잔여 HP 비율
// result.SideBBaseHpRatio   → SideB 기지 잔여 HP 비율

// 클라이언트 victory/defeat 해석 예시:
bool isLocalVictory = result.WinnerSide == BattleSide.SideA; // localSide = SideA인 경우
ShowResult(isLocalVictory);
```

---

## 파일 구조

```
fixtures/
  configs/             BattleConfigSnapshot 값 (side_a, side_b 각각의 energy/hp 포함)
  initial_states/      BattleInitialState 값 (side_a.slots, side_b.slots 포함)
  input_logs/          BattleCommand 배열 (tick 오름차순, side 필드 포함)
  expected_results/    BattleResult 단언 값 (winner_side, side_a/b_base_hp_ratio)
```

## Fp 표기 규칙

JSON의 HP·에너지·공격력은 `"20.0000"` 형식 (Scale=10000 고정소수점 십진 표현).  
내부 raw 값 변환: `raw = decimal * 10000`.  
예: `"50.0000"` → raw `500000` → `Fp.FromInt(50)`.

---

## 검증 커맨드

```
dotnet build  .../BattleSim.Core.sln
dotnet run --project .../BattleSim.Core.Tests/BattleSim.Core.Tests.csproj --no-build
```

통과 출력:

```
Fp tests passed.
RNG tests passed.
Simulator + smoke tests passed.
BattleSim.Core all checks passed.
```
