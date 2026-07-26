# VOID SCRAPPER 오브젝트 물리/부유 패치 적용법

## 1. 변경 파일

수정:
- Core/MeteorObstacle.cs
- HarvestObjectHealth.cs
- Player/Bullet.cs
- Shop/ShopStructure.cs
- RunRuntime/ExpeditionEventObject.cs

추가:
- Core/SpaceDriftBody2D.cs
- VFX/WorldObjectVisualMotion2D.cs

## 2. 패치 핵심 규칙

- 대형 운석, 고가치 잔해, 파괴된 선체, 상점, 이벤트, 코어는 판정 Root를 고정합니다.
- 소형 운석과 작은 장식 잔해만 Rigidbody2D로 실제 이동합니다.
- 고정 오브젝트는 VisualRoot 자식만 부유/회전합니다.
- 적 탄환은 수확 오브젝트와 운석에 막히지만, 기본값으로 해당 오브젝트에 피해를 주지 않습니다.
- 적 탄환은 상점을 공격하거나 적대화시키지 않습니다.
- 상점은 Rigidbody2D가 실수로 붙어 있어도 기본값으로 Kinematic + FreezeAll 처리됩니다.
- 이벤트의 Animated Root가 루트 Transform으로 연결되어 있으면 자동으로 VisualRoot 자식을 찾으며, 판정 Root 자체의 부유를 막습니다.

---

# 3. 레이어 권장 구성

다음 레이어를 추가하는 것을 권장합니다.

- PlayerBody
- EnemyBody
- WorldSolid
- DriftDebris
- HarvestSoft
- HarvestSolid
- ShopBody
- Interactable
- PlayerProjectile
- EnemyProjectile
- Pickup
- EventDamage

Physics 2D Layer Collision Matrix 권장값:

- PlayerBody ↔ EnemyBody: OFF
- EnemyBody ↔ EnemyBody: OFF
- PlayerBody ↔ WorldSolid: ON
- PlayerBody ↔ DriftDebris: ON
- PlayerBody ↔ HarvestSoft: ON
- PlayerBody ↔ HarvestSolid: ON
- PlayerBody ↔ ShopBody: ON
- EnemyBody ↔ WorldSolid: ON
- EnemyBody ↔ DriftDebris: OFF
- EnemyBody ↔ HarvestSoft: OFF
- EnemyBody ↔ HarvestSolid: ON
- PlayerProjectile ↔ WorldSolid: ON
- PlayerProjectile ↔ DriftDebris: ON
- PlayerProjectile ↔ HarvestSoft: ON
- PlayerProjectile ↔ HarvestSolid: ON
- PlayerProjectile ↔ ShopBody: ON
- PlayerProjectile ↔ EventDamage: ON
- EnemyProjectile ↔ WorldSolid: ON
- EnemyProjectile ↔ DriftDebris: ON
- EnemyProjectile ↔ HarvestSoft: ON
- EnemyProjectile ↔ HarvestSolid: ON
- EnemyProjectile ↔ ShopBody: ON
- Pickup ↔ PlayerBody: ON
- Interactable ↔ PlayerBody: ON

주의:
- Trigger도 Layer Collision Matrix가 꺼져 있으면 감지되지 않습니다.
- PlayerBody ↔ EnemyBody를 끄면 서로 겹칠 수 있으므로 적 AI의 Separation 보정 또는 별도 Contact Trigger를 나중에 추가하는 것이 좋습니다.

---

# 4. 공통 Root / VisualRoot 구조

고정 오브젝트는 아래 구조로 통일합니다.

```text
PF_Object
├─ Root
│  ├─ Collider2D
│  ├─ RadarTarget / IInteractable / Health 등 판정 컴포넌트
│  └─ RewardDropper 등
└─ VisualRoot
   ├─ SpriteRenderer
   └─ WorldObjectVisualMotion2D
```

중요:
- Collider2D, RadarTarget, 상호작용 스크립트는 Root에 둡니다.
- WorldObjectVisualMotion2D는 VisualRoot에 둡니다.
- Root에는 WorldObjectVisualMotion2D를 붙이지 않습니다.
- VisualRoot 이름을 그대로 `VisualRoot`로 사용하면 이벤트가 자동으로 찾습니다.

---

# 5. 대형 운석 프리팹

권장 구조:

```text
PF_Meteor_Large
├─ Root
│  ├─ MeteorObstacle
│  ├─ PolygonCollider2D 또는 CircleCollider2D
│  └─ RadarTarget
└─ VisualRoot
   ├─ SpriteRenderer
   └─ WorldObjectVisualMotion2D
```

Root:
- Layer: WorldSolid
- Rigidbody2D: 제거 권장
- Collider2D Is Trigger: OFF
- Collider 크기: 이미지 외곽의 약 80~90%

MeteorObstacle:
- Motion Mode: Static Terrain
- Max HP: 현재 밸런스 유지 또는 3~8
- Take Damage From Player Projectiles: ON
- Take Damage From Enemy Projectiles: OFF
- Block Projectile When Damage Ignored: ON
- Visual Root: VisualRoot
- Allow Root Visual Rotation When Visual Root Missing: OFF

WorldObjectVisualMotion2D:
- Use Float: OFF 또는 Amplitude 0.01~0.02
- Use Continuous Rotation: ON
- Rotation Speed Range: -3 ~ 3
- Use Rotation Sway: OFF
- Use Scale Pulse: OFF

결과:
- 운석 판정 위치는 고정됩니다.
- 플레이어와 탄환의 엄폐물 역할을 유지합니다.
- 비주얼만 느리게 회전합니다.

---

# 6. 소형 운석 프리팹

권장 구조:

```text
PF_Meteor_Small
├─ Rigidbody2D
├─ CircleCollider2D 또는 PolygonCollider2D
├─ MeteorObstacle
├─ SpaceDriftBody2D
├─ RadarTarget
└─ VisualRoot
   └─ SpriteRenderer
```

Layer:
- DriftDebris

Rigidbody2D:
- Body Type: Dynamic
- Material: SpaceDebrisMaterial
- Mass: 0.8~1.5
- Linear Damping: 0.8~1.4
- Angular Damping: 0.4~0.8
- Gravity Scale: 0
- Collision Detection: Continuous
- Interpolate: Interpolate
- Constraints: None

PhysicsMaterial2D `SpaceDebrisMaterial`:
- Friction: 0
- Bounciness: 0.05~0.10

MeteorObstacle:
- Motion Mode: Rigidbody Drift
- Drift Body: 같은 오브젝트의 SpaceDriftBody2D
- Min Drift Speed: 0.15
- Max Drift Speed: 0.45
- Min Spin Speed: -25
- Max Spin Speed: 25
- Hit Impulse: 0.25~0.4
- Take Damage From Player Projectiles: ON
- Take Damage From Enemy Projectiles: OFF
- Block Projectile When Damage Ignored: ON

SpaceDriftBody2D:
- Configure Rigidbody On Enable: ON
- Mass: 1
- Linear Damping: 1
- Angular Damping: 0.5
- Speed Range: 0.15 / 0.45
- Angular Speed Range: -25 / 25
- Max Linear Speed: 2
- Max Angular Speed: 90
- Bounds Padding: 0.5
- Bounce Velocity Retention: 0.8

주의:
- 기존 MeteorObstacle 프리팹은 호환성을 위해 Motion Mode 기본값이 Legacy Transform Drift입니다.
- 새 물리를 사용하려면 반드시 소형 운석의 Motion Mode를 Rigidbody Drift로 직접 변경하세요.
- SpaceDriftBody2D는 ExpeditionMapGenerator가 MeteorObstacle.SetRoamingBounds를 호출하면 같은 Bounds를 자동 전달받습니다.

---

# 7. 보급 컨테이너

권장 구조:

```text
PF_SupplyContainer
├─ Root
│  ├─ HarvestObjectHealth
│  ├─ RewardDropper
│  ├─ RadarTarget
│  └─ CapsuleCollider2D 또는 PolygonCollider2D
└─ VisualRoot
   ├─ SpriteRenderer
   └─ WorldObjectVisualMotion2D
```

Root:
- Layer: HarvestSoft
- Rigidbody2D: 제거
- Collider Is Trigger: OFF
- Collider 크기: 이미지의 약 60~70%

HarvestObjectHealth:
- Object Kind: SupplyContainer
- Take Damage From Player Projectiles: ON
- Take Damage From Enemy Projectiles: OFF
- Block Projectile When Damage Ignored: ON
- Receive Dash Knockback: OFF
- Require Dynamic Rigidbody For Knockback: ON

WorldObjectVisualMotion2D:
- Use Float: ON
- Float Axis: 0, 1
- Float Amplitude: 0.04~0.07
- Float Duration: 2.0~2.8
- Use Continuous Rotation: OFF
- Use Rotation Sway: ON
- Rotation Sway Degrees: 0.5~1.0
- Rotation Sway Duration: 2.5~3.5
- Use Scale Pulse: OFF

Collider가 플레이어 이동을 너무 방해하면:
- Rigidbody를 붙이지 말고 Collider 크기를 더 줄입니다.
- 사각형 모서리에 걸리면 BoxCollider2D 대신 CapsuleCollider2D 또는 PolygonCollider2D를 사용합니다.

---

# 8. 고가치 잔해

Root:
- Layer: HarvestSolid
- Rigidbody2D: 제거
- Collider Is Trigger: OFF
- Collider 크기: 이미지의 약 75~85%

HarvestObjectHealth:
- Object Kind: HighValueWreck
- Take Damage From Player Projectiles: ON
- Take Damage From Enemy Projectiles: OFF
- Block Projectile When Damage Ignored: ON
- Receive Dash Knockback: OFF

WorldObjectVisualMotion2D:
- Float Amplitude: 0.02~0.04
- Float Duration: 3~4
- Continuous Rotation: ON
- Rotation Speed Range: -1.5~1.5
- Rotation Sway: OFF

고가치 잔해는 디펜더의 보호 위치이므로 Root를 실제로 이동시키지 않습니다.

---

# 9. 파괴된 선체

권장 구조:

```text
PF_DestroyedHull
├─ Root
│  ├─ HarvestObjectHealth
│  ├─ RewardDropper
│  ├─ RadarTarget
│  └─ PolygonCollider2D
└─ VisualRoot
   ├─ MainHullSprite
   ├─ LoosePanel_A
   ├─ LoosePanel_B
   └─ SmallDebrisOrbit
```

Root:
- Layer: HarvestSolid
- Rigidbody2D: 제거
- Receive Dash Knockback: OFF

움직임:
- 본체 전체 Float: 0~0.02
- 본체 회전: 초당 0~0.6도
- LoosePanel 자식에 별도 WorldObjectVisualMotion2D 사용
- LoosePanel Rotation Sway: 1~2도
- 작은 파편만 Continuous Rotation 사용

전체 선체를 크게 흔들지 말고, 일부 파츠만 움직이는 것이 자연스럽습니다.

---

# 10. 이벤트 오브젝트

권장 구조:

```text
PF_Event
├─ Root
│  ├─ ExpeditionEventObject
│  ├─ RadarTarget
│  ├─ RewardDropper
│  └─ Interaction Collider (Trigger)
├─ VisualRoot
│  ├─ SpriteRenderer
│  ├─ RingRoot
│  └─ ProgressRoot
├─ PromptAnchor
└─ RewardDropPoint
```

설정:
- Root는 이동시키지 않습니다.
- Visual Root = VisualRoot
- Animated Root = VisualRoot
- Prevent Animated Root From Using Event Root: ON
- Root Collider: Is Trigger ON
- Rigidbody2D: 제거

이번 패치부터 Animated Root가 Root 자신으로 연결되어 있어도 `VisualRoot` 자식을 우선 찾습니다.
VisualRoot 자식이 없으면 Root 위치 부유를 막기 위해 Animated Root를 사용하지 않습니다.

이벤트별 권장값:

구조 신호:
- Idle Float Y: 0.06
- Idle Float Duration: 1.8
- Idle Pulse Scale: 1.03

미확인 장치:
- Idle Float Y: 0.03
- Idle Float Duration: 2.2
- Idle Pulse Scale: 1.05

불안정 원자로:
- Idle Float Y: 0
- Idle Pulse Scale: 1.02
- 내부 CoreRenderer/Ring만 연출

블랙박스:
- Idle Float Y: 0.03
- Idle Float Duration: 2.5
- VisualRoot에 ±1~2도 회전 흔들림 권장

---

# 11. 불안정 원자로

```text
Event_UnstableReactor
├─ Root
│  ├─ ExpeditionEventObject
│  └─ InteractionCollider (Trigger)
├─ VisualRoot
└─ DamageReceiver
   ├─ Collider2D (Trigger, 시작 시 Disabled)
   └─ ExpeditionEventDamageReceiver
```

- Root Layer: Interactable
- DamageReceiver Layer: EventDamage
- PlayerProjectile ↔ EventDamage: ON
- EnemyProjectile ↔ EventDamage: 필요 없으면 OFF
- Animated Root: VisualRoot
- Rigidbody2D: 제거

---

# 12. 상점 구조선

권장 구조:

```text
PF_ShopStructure
├─ Root
│  ├─ ShopStructure
│  ├─ BodyCollider
│  ├─ ShieldHitbox
│  └─ RadarTarget
├─ InteractionArea
│  └─ CircleCollider2D (Trigger)
└─ VisualRoot
   ├─ SpriteRenderer
   └─ WorldObjectVisualMotion2D
```

Root:
- Layer: ShopBody
- Rigidbody2D: 제거 권장
- BodyCollider: Is Trigger OFF

InteractionArea:
- Layer: Interactable
- CircleCollider2D: Is Trigger ON
- 반경: 본체보다 약 0.8~1.2 크게

ShopStructure Physics Safety:
- Allow External Knockback: OFF
- Enforce Immovable Rigidbody: ON

WorldObjectVisualMotion2D:
- Float Amplitude: 0.02~0.04
- Float Duration: 2.5~3.5
- Rotation Sway: 0.3~0.7도
- Continuous Rotation: OFF

주의:
- 상호작용을 위해 본체 충돌을 없앨 필요가 없습니다.
- BodyCollider는 통과 방지, InteractionArea는 E 상호작용 감지용입니다.
- ShopStructure의 Shield Break Knockback Layer에서 ShopBody는 제외하세요.

---

# 13. 코어

권장 구조:

```text
PF_Core
├─ Root
│  ├─ CoreObject
│  ├─ BodyCollider
│  └─ Interaction Collider
└─ VisualRoot
   ├─ CoreBody
   ├─ InnerRing
   └─ OuterRing
```

- Root: 고정, Rigidbody2D 제거
- CoreBody: 작은 Scale Pulse
- InnerRing/OuterRing: 서로 반대 방향 Continuous Rotation
- Root 자체 Float: 0 또는 최대 0.01~0.02
- 상호작용/보스전 중심 좌표가 변하면 안 됩니다.

---

# 14. 보상 드랍

보상 드랍은 실제 물리를 사용해도 됩니다.

권장 Rigidbody2D:
- Body Type: Dynamic 또는 Kinematic 기반 현재 구조 유지
- Gravity Scale: 0
- Linear Damping: 2~4
- Angular Damping: 1~3
- Collider: Trigger ON
- PlayerBody와 Trigger 감지
- WorldSolid과 충돌은 게임 체감에 따라 OFF 권장

보상은 플레이어에게 흡수되는 오브젝트이므로 상점/이벤트처럼 고정할 필요가 없습니다.

---

# 15. 플레이어 / 적 충돌

권장:
- PlayerBody ↔ EnemyBody: OFF
- EnemyBody ↔ EnemyBody: OFF

이유:
- 플레이어와 적 모두 Rigidbody2D 속도를 직접 제어하므로 물리 충돌을 켜면 떨림과 끼임이 발생하기 쉽습니다.
- 적끼리 하드 충돌하면 좁은 통로에 뭉쳐 길을 막습니다.

초기 프로토타입에서는 접촉 피해 없이 통과시키는 것이 가장 안정적입니다.
필요하면 이후 별도 Contact Trigger와 작은 Separation Force를 추가하세요.

---

# 16. 탄환 규칙

이번 패치 적용 후 기본값:

플레이어 탄환:
- 수확 오브젝트 피해: O
- 운석 피해: O
- 상점 피해/적대화: O
- 이벤트 원자로 피해: O

적 탄환:
- 수확 오브젝트에 막힘: O
- 수확 오브젝트 피해: X
- 운석에 막힘: O
- 운석 피해: X
- 상점에 막힘: O
- 상점 피해/적대화: X
- 이벤트 원자로 피해: X

각 수확 오브젝트/운석에서 필요하면 Enemy Projectile Damage 옵션을 개별적으로 켤 수 있습니다.

---

# 17. 적용 순서

1. 패치 파일을 기존 Scripts 폴더에 덮어씁니다.
2. Unity Console의 컴파일 오류를 먼저 확인합니다.
3. 모든 운석 프리팹의 MeteorObstacle Motion Mode를 확인합니다.
4. 대형 운석은 Static Terrain으로 변경합니다.
5. 소형 운석에는 Rigidbody2D + SpaceDriftBody2D를 추가하고 Rigidbody Drift로 변경합니다.
6. 컨테이너/잔해/선체/상점/이벤트/코어에서 불필요한 Rigidbody2D를 제거합니다.
7. 각 고정 오브젝트에 VisualRoot 자식을 만듭니다.
8. VisualRoot에 WorldObjectVisualMotion2D를 추가합니다.
9. 수확 오브젝트의 Receive Dash Knockback을 OFF로 설정합니다.
10. 상점의 BodyCollider와 InteractionArea를 분리합니다.
11. 이벤트의 Animated Root를 VisualRoot에 연결합니다.
12. Physics 2D Layer Collision Matrix를 정리합니다.
13. 플레이 모드에서 아래 검수표를 확인합니다.

---

# 18. 플레이 검수표

- 대형 운석이 위치는 고정되고 비주얼만 회전하는가
- 소형 운석이 천천히 이동하며 플레이어 충돌 시 약하게 밀리는가
- 소형 운석이 맵 경계 안에서 반사되는가
- 컨테이너가 플레이어에게 밀리지 않는가
- 컨테이너 Visual만 위아래로 움직이는가
- 고가치 잔해와 디펜더 보호 위치가 어긋나지 않는가
- 이벤트 상호작용 Collider 위치가 움직이지 않는가
- 상점 본체는 막히지만 InteractionArea에서 E가 뜨는가
- 상점이 대쉬/보호막 폭발로 밀리지 않는가
- 적 탄환이 컨테이너를 파괴하지 않는가
- 적 탄환이 상점을 적대화시키지 않는가
- 플레이어 탄환은 수확 오브젝트와 상점을 정상 공격하는가
- PlayerBody ↔ EnemyBody 충돌 해제 후 떨림이 줄었는가
