# Strange lighting after Menu → Current

## Can I see your Menu scene?
**No.** This GitHub repo does not contain `Menu.unity`, `Current.unity`, or your local load script—only scripts agents have added. To inspect the real scene, push those files (or paste the load script + a screenshot of Window → Rendering → Lighting for both scenes).

## Most likely causes

### 1. Stale environment lighting after `LoadScene` (very common)
Pressing Play **inside** `Current` in the Editor applies that scene’s skybox/ambient correctly.  
Loading `Current` **from Menu** at runtime can leave a stale ambient probe / reflections until refreshed.

**Fix:** use `SceneLoadLightingFix` (auto-runs) + `TitleScreenController` which calls `DynamicGI.UpdateEnvironment()` after load.

### 2. Menu Directional Light surviving (`DontDestroyOnLoad`)
If Menu has a sun (or a bootstrap marked DontDestroyOnLoad), you can end up with **2 directional lights** in Current → washed out / double shadows.

**Check:** after clicking Play, in Hierarchy search `Light`. You should only see Current’s sun.  
`SceneLoadLightingFix` logs a warning if more than one directional light is enabled.

### 3. Additive load instead of Single
`LoadScene("Current", LoadSceneMode.Additive)` keeps Menu’s fog/sky/lights alive.

**Fix:** always load with `LoadSceneMode.Single` (default in updated `TitleScreenController`).

### 4. Current has no lighting data when entered via build flow
- Window → Rendering → Lighting
- For **Current**: Environment skybox assigned, Ambient Source set, and either:
  - Auto Generate on, or
  - Generate Lighting baked and saved

If Current looks fine only when you press Play from that scene, compare Lighting window values vs Menu (Menu often uses flat dark ambient for UI).

### 5. URP Global Volume from Menu
A Volume with `DontDestroyOnLoad` (exposure/bloom/color) can make Current look wrong.

**Check:** no Volume/GameManager from Menu surviving into Current.

## What to do now
1. Copy `SceneLoadLightingFix.cs` and updated `TitleScreenController.cs` into your project
2. On Menu Play button → `TitleScreenController.OnPlayPressed` (scene name `Current`)
3. Build Settings includes `Menu` and `Current`
4. Play from Menu and check Console for multi-light warnings

## If it’s still wrong
Paste here:
- your Menu load script
- whether Current looks correct when you press Play **while Current is open**
- screenshot / description (too dark, too bright, no shadows, pink, flat ambient)
