using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using TMPro;
using Object = UnityEngine.Object;

// Explicit opt-in Play Mode fixture. Never saves scenes or the user's save.
[InitializeOnLoad]
public static class DefenseOverseerProbe
{
    const string SessionKey = "DefenseOverseer.Probe", PresentationKey = "DefenseOverseer.PresentationProbe";
    static string Dir => SessionState.GetBool(PresentationKey,false) ? "Logs/DefenseOverseer/Presentation/" : "Logs/DefenseOverseer/";
    static readonly List<string> log = new List<string>(), errors = new List<string>(), motion = new List<string>();
    static IEnumerator<float> sequence;
    static double next, end;
    static RenderTexture target;
    static Camera camera;
    static PlayerHealth player;
    static FrigateTriadBossController boss;
    static EnemyHealth bossHealth;
    static bool autoFire;
    static string weaponLabel;
    static float lastSample = -1;
    static FrigateTriadBossController.DefenseStage lastStage;
    static int lastCycle;
    static float stageStart;
    static readonly HashSet<string> captured = new HashSet<string>();
    static bool finishing;
    static UnityEngine.InputSystem.Mouse qaMouse;
    static Keyboard qaKeyboard;
    static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    static float nextInputDiagnostic;
    static DefenseOverseerProbe()
    {
        EditorApplication.playModeStateChanged += Changed;
        if (SessionState.GetBool(SessionKey, false)) Application.logMessageReceived += OnLog;
    }
    static void OnLog(string m, string s, LogType t)
    { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(m + "\n" + s); }
    public static void Run() => BeginRun(false);
    public static void RunPresentation() => BeginRun(true);
    static void BeginRun(bool presentation)
    {
        SessionState.SetBool(PresentationKey,presentation);
        ApprovedVisualIntegration.Guard(); Directory.CreateDirectory(Dir + "Rendered");
        SetNativeGameView();
        File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json", true);
        File.Copy("Logs/SectorAdministrator/fresh-save.json", Dir + "probe-save.json.bak", true);
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Boot.unity");
        var save = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SaveManager>(true)).Single();
        var so = new SerializedObject(save); so.FindProperty("fileName").stringValue = Path.GetFullPath(Dir + "probe-save.json"); so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(SessionKey, true); EditorApplication.EnterPlaymode();
    }
    static void SetNativeGameView()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, new object[] { Enum.ToObject(kind, 1), 480, 270, "Combat QA native" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, count - 1);
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            qaMouse = InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("SectorQAMouse");
            qaKeyboard = InputSystem.AddDevice<Keyboard>("SectorQAKeyboard");
            sequence = Check().GetEnumerator(); end = EditorApplication.timeSinceStartup + 2000; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Note("Console errors including teardown=" + errors.Count); Flush(); SessionState.SetBool(SessionKey, false); EditorApplication.Exit(errors.Count == 0 ? 0 : 1); }
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (finishing) return;
        try
        {
            SampleCombat();
            if (EditorApplication.timeSinceStartup < next) return;
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Probe timeout");
            if (sequence.MoveNext()) next = EditorApplication.timeSinceStartup + sequence.Current;
            else Finish();
        }
        catch (Exception e) { errors.Add(e.ToString()); Finish(); }
    }
    static void Finish()
    {
        finishing = true; EditorApplication.update -= Tick;
        if (qaMouse != null) InputSystem.RemoveDevice(qaMouse);
        if (qaKeyboard != null) InputSystem.RemoveDevice(qaKeyboard);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        Flush(); EditorApplication.ExitPlaymode();
    }
    static void Flush() { File.WriteAllLines(Dir + "playmode.txt", log); File.WriteAllLines(Dir + "errors.txt", errors); File.WriteAllLines(Dir + "combat-timing.csv", motion); }
    static void Note(string value) { log.Add(DateTime.UtcNow.ToString("HH:mm:ss") + " " + value); Flush(); }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Note("PASS " + message); }
    static IEnumerable<float> Until(Func<bool> check, string label, float seconds = 80)
    { double deadline = EditorApplication.timeSinceStartup + seconds; while (!check()) { if (EditorApplication.timeSinceStartup > deadline) throw new Exception(label); yield return .02f; } Note("REACHED " + label); }
    static IEnumerable<float> Scene(string name)
    { foreach (var d in Until(() => SceneManager.GetActiveScene().name == name && !SceneFlowManager.Instance.IsLoading, name)) yield return d; yield return 3; BindCamera(); }
    static void BindCamera()
    {
        camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (target == null) { target = new RenderTexture(480, 270, 24) { antiAliasing = 1 }; target.Create(); }
        camera.targetTexture = target;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
            {
                // Camera.Render excludes Overlay canvases. Preserve their original precedence
                // when adapting them for this disposable native render target.
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) c.sortingOrder += 10000;
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1;
            }
        Canvas.ForceUpdateCanvases();
    }
    static void Capture(string name)
    {
        BindCamera(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(480, 270, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); png.Apply();
        File.WriteAllBytes(Dir + "Rendered/" + name + ".png", png.EncodeToPNG()); Object.Destroy(png); RenderTexture.active = previous;
        File.WriteAllLines(Dir + "Rendered/" + name + "-text.txt", Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .Where(t => !string.IsNullOrWhiteSpace(t.text)).Select(t => t.name + " overflow=" + t.isTextOverflowing + " rect=" + t.rectTransform.rect + " text=" + t.text.Replace('\n', '|')));
        File.WriteAllLines(Dir + "Rendered/" + name + "-sprites.txt", Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && !r.forceRenderingOff && camera.WorldToViewportPoint(r.bounds.center).x > -.1f && camera.WorldToViewportPoint(r.bounds.center).x < 1.1f)
            .Select(r => r.name + " sprite=" + AssetDatabase.GetAssetPath(r.sprite) + " bounds=" + r.bounds + " viewport=" + camera.WorldToViewportPoint(r.bounds.center)));
        Note("CAPTURE " + name + " 480x270");
        Note("Screen=" + Screen.width + "x" + Screen.height + "; canvases=" + string.Join(";", Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).Select(c => c.name + " scale=" + c.scaleFactor + " rect=" + ((RectTransform)c.transform).rect)));
    }
    static void Ready()
    { Keys(); player = Object.FindFirstObjectByType<PlayerHealth>(); Require(player != null, "authored player"); player.SetDashInvincible(true); }
    static void Move(Vector3 p)
    { var rb = player.GetComponent<Rigidbody2D>(); rb.position = p; player.transform.position = p; rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
    static void Mouse(bool pressed) => InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new MouseState { position = new Vector2(650, 400) }.WithButton(MouseButton.Left, pressed));
    static IEnumerable<float> Dialogue()
    {
        double deadline = EditorApplication.timeSinceStartup + 90;
        while (DialogueManager.isConversationActive)
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Dialogue timeout");
            var state = DialogueManager.instance.currentConversationState;
            if (state != null && state.hasPCResponses) DialogueManager.instance.conversationView.SelectResponse(new SelectedResponseEventArgs(state.pcResponses[0]));
            else DialogueManager.instance.conversationView?.OnConversationContinueAll();
            yield return .2f;
        }
    }

    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static FrigateBossPart targetPart;
    static int previousAlive, finalDeathCount;
    static Vector2 lockedZonePoint;
    static DefenseBarrageZone observedZone;
    static int observedZoneId;
    static float phaseStart;
    static object Get(object o,string field) => o.GetType().GetField(field,Flags).GetValue(o);
    static void SampleCombat()
    {
        if (boss == null || bossHealth == null) return;
        var stage=boss.CurrentDefenseStage;
        if (Time.time != lastSample)
        {
            lastSample=Time.time;
            var zones=Object.FindObjectsByType<DefenseBarrageZone>(FindObjectsSortMode.None);
            int warnings=zones.Count(z=>z.IsWarning), damaging=zones.Count(z=>z.IsDamaging), visible=zones.Count(z=>z.IsVisible);
            int projectiles=Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b=>b.SourceRoot==boss.transform);
            if(warnings>1 || damaging>1 || visible>1)throw new Exception("Concurrent artillery zones exceeded one");
            if(zones.Any(z=>z.IsDamaging && (!z.IsVisible||z.IsWarning)))throw new Exception("Invisible or early artillery collision");
            if(boss.IsGameplayActive && boss.ActivePattern!=SalvageDevourerCombatPattern.None && boss.ActivePattern!=SalvageDevourerCombatPattern.BatterySuppression && boss.ActivePattern!=SalvageDevourerCombatPattern.HeavyBarrage)
                throw new Exception("Legacy attack scheduled");
            foreach(var z in zones.Where(z=>z.IsVisible))
            {
                if(observedZone!=z || observedZoneId!=boss.DefenseCycle || !z.IsWarning) {observedZone=z;observedZoneId=boss.DefenseCycle;lockedZonePoint=z.CommittedPosition;}
                if((Vector2)z.transform.position!=z.CommittedPosition)throw new Exception("Barrage moved away from committed position");
            }
            if(boss.State==FrigateTriadBossState.Transition && (visible>0 || projectiles>0))throw new Exception("Attack survived Armor Break/reformation");
            if(boss.IsGameplayActive)
            {
                var hud=Object.FindFirstObjectByType<ExpeditionHUD>();
                if(hud!=null && (!hud.IsOperationBriefingSuppressed || ((GameObject)Get(hud,"operationRoot")).activeSelf))
                    throw new Exception("Exploration briefing overlaps active Region B combat");
                foreach(var enemy in Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None))
                    if((bool)typeof(EnemyBaseAI).GetMethod("IsAmbientEnemyForBossEncounterIsolation",Flags).Invoke(enemy,null) &&
                        (!enemy.IsBossEncounterIsolated || enemy.GetComponentsInChildren<Renderer>(true).Any(r=>!r.forceRenderingOff)))
                        throw new Exception("Visible ordinary enemy during Region B: "+enemy.name);
                foreach(var meteor in Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None))
                    if(!meteor.IsSectorEncounterPaused)throw new Exception("Meteor resumed during Region B");
            }
            motion.Add(weaponLabel+","+Time.time.ToString("F4")+","+boss.DefenseCycle+","+stage+","+boss.AlivePartCount+","+boss.IsArmorBroken+","+boss.ArmorBreakCount+","+warnings+","+damaging+","+visible+","+boss.DefenseShots+","+bossHealth.CurrentHp.ToString("F3")+","+projectiles+","+boss.LastBatteryOwner?.PartId+","+boss.LastBatteryWasRight+","+boss.State);
            if(previousAlive!=boss.AlivePartCount)
            {
                previousAlive=boss.AlivePartCount;
                Note("PART COUNT "+previousAlive+" HP="+bossHealth.CurrentHp+" armorBreakCount="+boss.ArmorBreakCount);
            }
            if(SessionState.GetBool(PresentationKey,false) && boss.DefenseCycle>=2)
            {
                var flashes=(GameObject[])Get(boss,"defenseFlashes");
                for(int i=0;i<flashes.Length;i++)
                {
                    var f=flashes[i];if(f==null || !f.activeSelf)continue;
                    var sr=f.GetComponent<SpriteRenderer>();
                    string shotLabel=weaponLabel+"-"+(i%2==0?"left":"right")+"-battery-muzzle-peak";
                    if(sr.sprite!=null && sr.sprite.name.EndsWith("_00") && captured.Add(shotLabel))
                    {
                        Require(Vector2.Distance(f.transform.position,boss.DefenseMuzzle(i/2,i%2==1).position)<.05f,"live flash follows its firing module");
                        Capture(shotLabel);
                    }
                }
            }
            string label=weaponLabel+"-"+boss.AlivePartCount+"parts-"+stage;
            if(stage!=lastStage || lastCycle!=boss.DefenseCycle) {lastStage=stage;lastCycle=boss.DefenseCycle;stageStart=Time.time;}
            float age=Time.time-stageStart;
            if(age>.1f && (stage==FrigateTriadBossController.DefenseStage.LeftBattery || stage==FrigateTriadBossController.DefenseStage.RightBattery || stage==FrigateTriadBossController.DefenseStage.Recovery) && captured.Add(label))Capture(label);
            if(stage==FrigateTriadBossController.DefenseStage.BarrageWarning && age>.6f && captured.Add(label))Capture(label);
            if(damaging>0 && captured.Add(weaponLabel+"-"+boss.AlivePartCount+"parts-impact-damage"))Capture(weaponLabel+"-"+boss.AlivePartCount+"parts-impact-damage");
            if(stage==FrigateTriadBossController.DefenseStage.ArmorBreak && age>.55f && captured.Add(weaponLabel+"-armor-break"))Capture(weaponLabel+"-armor-break");
            if(stage==FrigateTriadBossController.DefenseStage.BarrageImpact && boss.AlivePartCount<3 && boss.DefenseCycle%2==0 && age>.08f && captured.Add(label+"-combined"))Capture(label+"-combined");
        }
        if(autoFire && player!=null && targetPart!=null && targetPart.IsAlive && boss.State!=FrigateTriadBossState.Transition)
        {
            // QA assistance keeps the player in weapon range as the actual corridor scrolls.
            Move(targetPart.transform.position+new Vector3(Mathf.Sin(Time.time*.7f)*.35f,-3.3f,0));
            Vector2 aim=camera.WorldToScreenPoint(targetPart.ResolveHomingAimPoint(player.transform.position));
            var tree=player.GetComponent<PlayerWeaponController>().CurrentWeaponTree;
            bool fire=tree==WeaponTreeType.MachineGun || (tree==WeaponTreeType.Sniper ? Time.time%1.2f<.85f : Time.time%.65f<.12f);
            qaMouse.MakeCurrent();InputSystem.QueueStateEvent(qaMouse,new MouseState {position=aim}.WithButton(MouseButton.Left,fire));
        }
        else Mouse(false);
    }
    static IEnumerable<float> Check()
    {
        motion.Add("weapon,time,cycle,stage,parts,broken,breakCount,warnings,damaging,visible,shots,hp,projectiles,lastOwner,rightBattery,healthState");
        foreach(var d in Until(()=>PermanentProgress.Instance!=null && SceneFlowManager.Instance!=null,"Boot owners"))yield return d;
        SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        bool presentationOnly=SessionState.GetBool(PresentationKey,false);
        foreach(var tree in presentationOnly ? new[]{WeaponTreeType.MachineGun} : new[]{WeaponTreeType.MachineGun,WeaponTreeType.Shotgun,WeaponTreeType.Sniper})
        {
            weaponLabel=tree.ToString();autoFire=false;targetPart=null;previousAlive=0;finalDeathCount=0;
            PermanentProgress.Instance.LoadFromSave(JsonUtility.FromJson<SaveData>(File.ReadAllText("Logs/SectorAdministrator/fresh-save.json")));
            PermanentProgress.Instance.RegisterCampaignBossDefeat(CampaignBossId.SectorAdministrator);
            PermanentProgress.Instance.TryAuthorizeAnalyzedRegion();
            Require(PermanentProgress.Instance.IsDepthUnlocked(ExpeditionDepth.DeepZone1),"isolated QA Region B route authorized");
            Require(!PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SalvageDevourer),"isolated Region B first clear");
            RunManager.Instance.StartNewRun(tree,ExpeditionDepth.DeepZone1);SceneFlowManager.Instance.LoadExpedition();
            foreach(var d in Scene("Expedition"))yield return d;Ready();yield return 1;
            var generator=Object.FindFirstObjectByType<ExpeditionMapGenerator>();var core=Object.FindFirstObjectByType<CoreObject>();Require(core!=null,"actual generated Region B Core");
            RevealTrackedCore();yield return 1;
            var data=generator.CurrentRegion2BossCorridor;
            var ambient=Object.FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None).FirstOrDefault();
            if(ambient!=null)ambient.transform.position=new Vector2(data.CenterX+2,data.TopY);
            var meteor=Object.FindFirstObjectByType<MeteorObstacle>();
            if(meteor!=null)meteor.transform.position=new Vector2(data.CenterX-2,data.TopY);
            Physics2D.SyncTransforms();Move(core.transform.position+Vector3.down*.6f);yield return .2f;
            Require(core.CanInteract(player.gameObject),"normal Core interaction accepted");core.Interact(player.gameObject);
            foreach(var d in Until(()=>
            {
                // Hold the QA player through the real stay-in-range activation timer.
                // Release this assistance as soon as activation hands movement to the intro.
                if(!(bool)Get(core,"activated"))
                {
                    Move(core.transform.position+Vector3.down*.6f);
                    if(core.CanInteract(player.gameObject))core.Interact(player.gameObject);
                }
                return Object.FindFirstObjectByType<FrigateTriadBossController>()!=null;
            },"Region B intro spawned real boss"))yield return d;
            boss=Object.FindFirstObjectByType<FrigateTriadBossController>();bossHealth=boss.AggregateHealth;bossHealth.Died+=_=>finalDeathCount++;
            foreach(var d in Until(()=>boss.IsEntryComplete,"three frigates entered camera"))yield return d;
            Capture(weaponLabel+"-intact-introduction");
            foreach(var d in Until(()=>boss.IsGameplayActive && boss.DefenseCycle>0,"live Region B scheduler"))yield return d;
            Require(boss.TotalMaxHealth==165 && boss.AlivePartCount==3,"three independent 55 HP targets");
            Require(player.GetComponent<PlayerWeaponController>().CurrentWeaponTree==tree,"actual equipped "+tree);
            if(meteor!=null)Require(meteor.IsSectorEncounterPaused && meteor.IsSectorEncounterHidden,"existing meteor hidden/frozen in corridor");
            Capture(weaponLabel+"-arena-clean");
            var pooledTemplate=AssetDatabase.LoadAssetAtPath<GameObject>(DefenseOverseerAuthoring.Zone);
            var pooled=PoolManager.Instance.Get(pooledTemplate,Vector3.one*1000,Quaternion.identity).GetComponent<DefenseBarrageZone>();
            pooled.BeginWarning(Vector2.one*1000,1.05f,0,null);pooled.SetImpactElapsed(.07f);PoolManager.Instance.Release(pooled.gameObject);
            Require(!pooled.IsVisible && !pooled.IsDamaging,"actual pooled OnDisable clears warning and damage");
            foreach(int count in new[]{3,2,1})
            {
                foreach(var d in Until(()=>boss.AlivePartCount==count && boss.State!=FrigateTriadBossState.Transition,"live "+count+" part formation"))yield return d;
                int startCycle=boss.DefenseCycle;
                // Observe two complete real cycles at each escalation before firing again.
                Keys(Key.D);yield return .35f;Keys();
                foreach(var d in Until(()=>boss.DefenseCycle>=startCycle+2,"two complete "+count+"-part cycles",120))yield return d;
                if(presentationOnly)
                {
                    Require(captured.Contains(weaponLabel+"-left-battery-muzzle-peak") && captured.Contains(weaponLabel+"-right-battery-muzzle-peak"),"both live module flash peaks captured");
                    foreach(var d in Until(()=>boss.CurrentDefenseStage==FrigateTriadBossController.DefenseStage.BarrageWarning,"interrupt a real active barrage warning"))yield return d;
                    bool resultReceived=false;RunEndReason resultReason=RunEndReason.None;
                    RunManager.Instance.RunEnded+=r=>{resultReceived=true;resultReason=r.endReason;};
                    float hp=bossHealth.CurrentHp;
                    player.SetDashInvincible(false);typeof(PlayerHealth).GetField("invincibleTimer",Flags).SetValue(player,0f);player.TakeDamage(99999);
                    Require(!boss.IsGameplayActive && bossHealth.CurrentHp==hp,"player death stops combat without killing boss");
                    Require(!Object.FindObjectsByType<DefenseBarrageZone>(FindObjectsSortMode.None).Any(z=>z.IsVisible||z.IsDamaging),"player death clears live barrage");
                    Require(FrigateTriadBossController.ActiveDefenseEncounter==null,"player death releases scoped isolation owner");
                    Require(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).All(m=>!m.IsSectorEncounterPaused),"player death restores meteor state");
                    Require(!PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SalvageDevourer),"player death grants no boss defeat or story reward");
                    foreach(var d in Until(()=>resultReceived,"existing player-death result authority",30))yield return d;
                    Require(resultReason==RunEndReason.Death,"existing Death result preserved");yield return 3;Capture("player-death-cleanup");
                    var resultPanel=Object.FindFirstObjectByType<RunResultPanelUI>();
                    Require(resultPanel!=null,"authored death result panel");resultPanel.Close();
                    foreach(var d in Scene("Settlement"))yield return d;
                    foreach(var d in Dialogue())yield return d;
                    Note("Completed dedicated live left/right flash and player-death interruption check; no boss victory was fabricated.");
                    yield break;
                }
                targetPart=((FrigateBossPart[])Get(boss,"parts")).First(p=>p.IsAlive);autoFire=true;
                foreach(var d in Until(()=>boss==null || bossHealth.IsDead || !targetPart.IsAlive,"actual "+tree+" destroys part "+targetPart.PartId,210))yield return d;
                autoFire=false;Mouse(false);
                if(count>1)
                {
                    Require(!targetPart.CanParticipateInPatterns,"destroyed part loses attack ownership immediately");
                    foreach(var d in Until(()=>boss.State!=FrigateTriadBossState.Transition,"safe transition completed"))yield return d;
                    Require(boss.ArmorBreakCount==1 && boss.IsArmorBroken,"one persistent Armor Break");
                    Capture(weaponLabel+"-"+(count-1)+"parts-broken");
                }
            }
            foreach(var d in Until(()=>boss==null || bossHealth.IsDead,"existing deferred final death",45))yield return d;
            autoFire=false;Mouse(false);Keys();yield return .15f;
            Require(finalDeathCount==1,"one aggregate death event");
            Require(PermanentProgress.Instance.HasDefeatedCampaignBoss(CampaignBossId.SalvageDevourer),"existing Region B campaign death authority");
            Require(!Object.FindObjectsByType<DefenseBarrageZone>(FindObjectsSortMode.None).Any(z=>z.IsVisible||z.IsDamaging),"no artillery survives death");
            Require(Object.FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None).All(m=>!m.IsSectorEncounterPaused),"ordinary meteor behavior restored");
            Require(FrigateTriadBossController.ActiveDefenseEncounter==null,"scoped arena owner released");
            Require(!Object.FindFirstObjectByType<ExpeditionHUD>().IsOperationBriefingSuppressed,"briefing suppression released after boss death");
            Capture(weaponLabel+"-death");yield return 8;Capture(weaponLabel+"-reward");
            Note("COMPLETED "+tree+": Boot-owned generated Region B -> normal Core interaction -> dedicated intro/corridor -> 3/2/1 living targets -> existing final charge/death/reward. Invulnerable QA player, assisted positioning, actual weapon inputs; not an unassisted balance playthrough.");
            boss=null;bossHealth=null;targetPart=null;
            SceneFlowManager.Instance.LoadSettlement();foreach(var d in Scene("Settlement"))yield return d;foreach(var d in Dialogue())yield return d;
        }
        Note("Three Region B first-clear runs complete at 480x270. QA scene return to Settlement; portal/reward-choice traversal not exercised. No user save or production scenes saved.");
    }
    static void RevealTrackedCore()
    {
        var tracking=Object.FindFirstObjectByType<CoreTrackingSignalController>();if(tracking==null || !tracking.IsTrackingActive)return;
        foreach(var wreck in Object.FindObjectsByType<HarvestObjectHealth>(FindObjectsSortMode.None))
        {if(tracking.IsCoreRevealed)break;if(wreck.ObjectKind==HarvestObjectKind.HighValueWreck)wreck.TakeDamage(99999);}
        Require(tracking.IsCoreRevealed,"Core revealed through real tracking events with QA instant wreck damage");
    }
}
