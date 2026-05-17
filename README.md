# 👻 Lost Souls

> *A prompt-based puzzle game where words are your only tool — and every explorer hears them differently.*

[![Unity](https://img.shields.io/badge/Unity-2022.3.56f1-black?logo=unity)](https://unity.com/)
[![Language](https://img.shields.io/badge/C%23-.NET-blueviolet?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![LLM](https://img.shields.io/badge/LLM-OpenAI-10A37F?logo=openai)](https://platform.openai.com/)
[![License](https://img.shields.io/badge/License-See%20LICENSE-yellow)](LICENSE)

---

## ✨ The Pitch

You are a kind spirit guiding **lost explorers** out of ancient ruin escape rooms. The only way to influence the world is to **type natural language prompts** — but manifesting in this realm costs energy, so every word counts.

Each explorer has their **own personality, perception quirks, and vocabulary**. One might call a key a *"shiny thing"*, another might refuse politely-worded requests, and another might confuse left and right. You don't control them — you *talk* to them.

🧠 The LLM interprets your words *through the character's mind*, then acts.

---

## 🎮 Core Loop

1. 📖 Read the level layout and the character's bio.
2. ⌨️  Type a natural language prompt (≤150 characters).
3. 🗣️  The character speaks back and acts — filtered through their quirks.
4. 🔁 Repeat until the objective is met or your prompt budget runs out.

---

## 🧩 Features

- 🤖 **Live LLM agents** — every explorer is driven by OpenAI's GPT, prompted with their unique profile.
- 🧙 **Personality system** — *polite, stubborn, unmotivated, impatient, distrustful, forgetful* quirks shape how characters respond.
- 👁️  **Perception filters** — *colorblind, mirrored view, size distortion, own vocabulary* warp what the character sees.
- 🧠 **Comprehension tiers** — `Simple` (1 action), `Standard` (2 actions + pathfind), `Clever` (3+ actions, conditionals).
- 🗺️  **Grid-based puzzles** — keys, doors, gems, pedestals, pressure plates, pushable boxes.
- 🎨 **8 unique character models** with full animation rigs (Idle / Walk / Pick / Push / Hold variants).
- 🛡️ **Layered safety** — input sanitization → LLM call → action validation, so prompt injection can't break the game.

---

## 📚 The 5 Levels

| # | Title | Character | Profile | Twist |
|---|-------|-----------|---------|-------|
| 1️⃣ | **Key to Freedom** | Sage 🧓 | Clever | Tutorial — say what you want |
| 2️⃣ | **Pick the Right One** | Sage 🧓 | Standard | Two keys, one door — specificity matters |
| 3️⃣ | **The Maze** | Reed 🧒 | Simple | Only understands directions, step by step |
| 4️⃣ | **Strange Words** | Pip 🦊 | Standard + Forgetful | Calls things by *their own names* |
| 5️⃣ | **Mind Your Manners** | Fern 🌿 | Polite | Won't lift a finger unless you ask nicely |

---

## 🏗️ Architecture at a Glance

```
Assets/Scripts/
├── 🧠 Core/         GameManager · LevelLoader · ObjectManager
├── 🗺️  Grid/         GridManager · Tile · Pathfinding (A*)
├── 🚶 Character/    ExplorerController · CharacterProfile
├── 🎒 Objects/      Key · Door · Gem · Pedestal · Box · PressurePlate
├── 💬 LLM/          LLMClient · PromptBuilder · ResponseParser
│                    InputSanitizer · ActionValidator · TurnManager
├── 🖼️  UI/           PromptInputUI · DialogueBubbleUI · EnergyBarUI
├── 🎞️  Animation/    CharacterModelManager · CameraController
└── 🛠️  Editor/       Scene + Prefab setup tools
```

**Data lives in JSON** (`Assets/Resources/Data/Levels/level_0X.json`) — every wall, door, key and character is declared explicitly, no hidden generation magic.

---

## ⚙️ Setup

### Prerequisites

- 🧊 **Unity Hub** + **Unity 2022.3.56f1** (LTS)
  *Other 2022 LTS revisions may work but are untested.*
- 🔑 An **OpenAI API key** with access to `gpt-4o-mini` (or change the model in config).
- 🧩 Git (with [Git LFS](https://git-lfs.com/) recommended — FBX & texture assets are bulky).

### 1. Clone the repo

```bash
git clone https://github.com/<your-username>/LostSouls.git
cd LostSouls
```

### 2. Open in Unity

1. Open **Unity Hub** → **Add project from disk** → select the cloned `LostSouls/` folder.
2. Make sure **Unity 2022.3.56f1** is installed (Unity Hub will offer to download it otherwise).
3. Let Unity import all assets — first import can take several minutes ☕.

### 3. Configure your API key 🔐

The OpenAI key is **not** committed to the repo. You need to create it locally.

Create a file at:

```
Assets/Resources/api_config.json
```

…with the following contents (use `Assets/Resources/api_config.example.json` as a starting point):

```json
{
  "openai_api_key": "sk-YOUR_KEY_HERE",
  "model": "gpt-4o-mini",
  "api_url": "https://api.openai.com/v1/chat/completions",
  "max_tokens": 300,
  "temperature": 0.7
}
```

> ⚠️ This file is **gitignored** — don't worry, you can't accidentally push your key.
> The game also looks for a fallback `api_config.template.json` in the project root.

### 4. Generate prefabs & set up the scene

From the Unity menu bar, run these editor commands **in order**:

1. `Lost Souls → Generate Prefabs from Models`  *(builds tile + object prefabs from the FBX models)*
2. `Lost Souls → Build Character Database`  *(scans `Assets/Characters/` and creates the animator + database)*
3. `Lost Souls → Setup Game Scene`  *(wires up GameManager, GridManager, UI, camera)*

### 5. Press ▶️

Open the main scene and hit **Play**. Level 1 loads by default.

---

## 🎹 Debug Keys (in Play mode)

> Only fire when no UI text field is focused.

| Key | Action |
|-----|--------|
| `1`–`5` | Load level 1 through 5 |
| `F` | Pick up adjacent object |
| `G` | Put down / place held object |
| `T` | Use held item (e.g. key on door) |
| `Shift + Arrow` | Push a box on the same tile |
| `Arrow keys` | Manual character movement |

---

## 🌳 Pushing to GitHub

This repository already includes a Unity-aware `.gitignore`. Before your first push:

```bash
# 1. Make sure you didn't add your API key by mistake
git status

# 2. (Optional but recommended) initialize Git LFS for big binary assets
git lfs install
git lfs track "*.fbx" "*.psd" "*.png" "*.jpg" "*.wav" "*.mp3"
git add .gitattributes

# 3. Commit & push
git add .
git commit -m "Initial public release 👻"
git branch -M main
git remote add origin https://github.com/<your-username>/LostSouls.git
git push -u origin main
```

🛑 **Double-check** that `Assets/Resources/api_config.json` is **NOT** in `git status` before pushing. If it is, your `.gitignore` is missing the entry.

---

## 🧪 Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| 🟥 *"LLMConfig: api_config.json not found"* | Create `Assets/Resources/api_config.json` (step 3 above). |
| 🟥 Characters stand still after a prompt | Invalid OpenAI key, or model name your account can't access. |
| 🟥 Tiles render as flat colored cubes | Run `Lost Souls → Generate Prefabs from Models` again. |
| 🟥 Character spawns as a capsule | Run `Lost Souls → Build Character Database`. |
| 🟥 Scene is empty when pressing Play | Run `Lost Souls → Setup Game Scene`. |
| 🟥 Debug keys don't work | Click somewhere outside the prompt InputField to defocus it. |

---

## 📜 License

See [LICENSE](LICENSE) for details.

---

## 🙏 Credits

- 🎨 3D models generated with **Tripo AI**
- 🤖 Character intelligence powered by **OpenAI GPT**
- 🛠️ Built in **Unity 2022 LTS** with the Universal Render Pipeline
- 💜 Made for the **Supercell AI Hackathon**

---

*Whisper kindly. They're listening.* 🕯️
