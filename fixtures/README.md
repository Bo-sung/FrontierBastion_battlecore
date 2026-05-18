# BattleSim.Core Smoke Fixtures

세 가지 canonical 시나리오로 Unity 클라이언트 연동을 빠르게 검증한다.

## 시나리오 요약

| 시나리오 | 커맨드 | 종료 tick | 결과 |
|---------|--------|-----------|------|
| player_victory | SpawnDroneSquad (tick 0) | 1 | Victory / EnemyBaseDestroyed |
| player_defeat | 없음 (enemy schedule만) | 1 | Defeat / PlayerBaseDestroyed |
| timeout_defeat | 없음 | 3 | Defeat / TimeOut |

**player_victory** — 드론 speed(2000) > lane 길이(1000) → tick 1에 enemy base(HP=1) 파괴.  
**player_defeat** — tick 1에 적 스폰, speed(2000) > lane 길이(1000) → player base(HP=1) 파괴.  
**timeout_defeat** — 커맨드/스폰 없음, maxBattleTick=3 도달, HP 동률 → Defeat.

---

## 클라이언트 tick loop 예시 (C#)

```csharp
// 1. config + initial state 구성 (fixtures의 JSON 값 참조)
BattleConfigSnapshot config = BuildConfig();
BattleInitialState initial  = BuildInitialState();

// 2. 시뮬레이터 생성
var sim = new BattleSimulator(config, initial);

// 3. tick loop
while (!sim.IsTerminated)
{
    // 현재 tick에 해당하는 플레이어 커맨드 제출
    foreach (BattleCommand cmd in GetCommandsForTick(sim.CurrentTick))
        sim.SubmitCommand(cmd);

    sim.AdvanceTick();

    // 상태 읽기 → Unity 비주얼 업데이트
    BattleState state = sim.GetState();
    UpdateVisuals(state);            // entity 위치, HP, 에너지 등
}

// 4. 결과 처리
BattleResult result = sim.GetResult();
// result.Outcome      → Victory | Defeat
// result.EndReason    → EnemyBaseDestroyed | PlayerBaseDestroyed | TimeOut
// result.ClearTimeTick→ 종료 tick
ShowResult(result);
```

---

## 파일 구조

```
fixtures/
  configs/             BattleConfigSnapshot 값
  initial_states/      BattleInitialState 값 (slot 스탯 포함)
  input_logs/          BattleCommand 배열 (tick 오름차순)
  expected_results/    BattleResult 단언 값
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
