# Title Screen Setup

Your Unity title scene isn’t in this git repo, so use these scripts inside your local project.

## Fix “I can’t see the title UI in Game view”

### Option A — one click (recommended)
1. Copy these scripts into your project:
   - `Assets/Scripts/UI/TitleScreenController.cs`
   - `Assets/Scripts/UI/CanvasFitToScreen.cs`
   - `Assets/Scripts/UI/Editor/TitleScreenSetupEditor.cs`
2. Open your title scene
3. Menu: **GameObject → UI → Create Fitted Title Screen**
4. Delete your old off-screen canvas if it’s still there
5. Save the scene

This creates:
- Screen Space Overlay canvas
- Canvas Scaler = **Scale With Screen Size** (1920×1080, match 0.5)
- Full-stretch background
- Centered title + Play + Quit
- Play wired to load scene **`Current`**

### Option B — fix your existing canvas
1. Select your Canvas
2. Add component **`CanvasFitToScreen`**
3. In Inspector, right-click the component → **Apply Canvas Fit**
4. Confirm Canvas settings:
   - Render Mode: **Screen Space - Overlay**
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920 × 1080**
   - Match: **0.5**
5. Select your background/root panel → anchors to stretch (hold Alt+Shift click the stretch preset), offsets all `0`
6. Make sure Game view aspect isn’t tiny / camera isn’t required (Overlay doesn’t need a camera)

## Wire Play → load `Current`

1. Select the object with **`TitleScreenController`**
2. Set **Play Scene Name** = `Current` (exact spelling)
3. Either:
   - Assign the Play button to the **Play Button** field, or
   - On the Play Button → OnClick (+) → drag the controller → choose **`TitleScreenController.OnPlayPressed`**

## Build Settings (required or LoadScene fails)
1. **File → Build Settings**
2. Add:
   - your title scene (index 0)
   - scene named exactly **`Current`**
3. Enable both checkboxes

## Quick test
1. Open title scene
2. Press Play
3. You should see the dark full-screen menu
4. Click **Play** → loads `Current`
