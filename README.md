# Mimesis InventoryExpansion Mod

This is a boilerplate template for creating new MelonLoader mods for Mimesis.

**Quick Start:** Clone this repository, rename everything to your mod name, set up a private Workspace repository with game DLLs, and start coding!

## HowToUse

### Prerequisites

Before you can use this boilerplate, you need:

1. **Software Requirements:**
   - [Mimesis](https://store.steampowered.com/app/2827200/MIMESIS/) (latest Steam build)
   - [.NET SDK 6.0+](https://dotnet.microsoft.com/download) (for building)
   - [Visual Studio 2022](https://visualstudio.microsoft.com/)
   - [MelonLoader](https://melonwiki.xyz/#/)
   - [Git](https://git-scm.com/) (for version control)

2. **Game Files:**
   - Access to your Mimesis installation directory
   - MelonLoader installed in the game

### Workspace Repository Setup

This boilerplate expects a **private Workspace repository** that contains shared dependencies. This keeps your mod repository clean and allows for easier dependency management.

#### 1. Create the Workspace Repository

Create a new **private** GitHub repository (e.g., `Mimesis-Workspace` or `Mimesis-ModWorkspace`).

#### 2. Workspace Repository Structure

Your Workspace repository should have the following structure:

```
Workspace/
├── lib/
│   ├── melonloader/
│   │   ├── MelonLoader.dll
│   │   └── 0Harmony.dll
│   └── game/
│       ├── Assembly-CSharp.dll
│       ├── UnityEngine.CoreModule.dll
│       ├── ... (all other game DLLs from MIMESIS_Data/Managed/)
│       └── (approximately 260 DLL files)
├── MimicAPI/
│   ├── GameAPI/
│   │   ├── ActorAPI.cs
│   │   ├── CoreAPI.cs
│   │   ├── PlayerAPI.cs
│   │   └── ... (other API files)
│   ├── MimicAPI.csproj
│   └── ... (other MimicAPI files)
├── scripts/
│   └── setup_lib.ps1
```

#### 3. Populate the Workspace Repository

**Option A: Using the Setup Script (Recommended)**

1. Clone your Workspace repository locally
2. Create a PowerShell script `scripts/setup_lib.ps1` with the following content (adjust paths to your Mimesis installation):

```powershell
$WorkspaceLib = Join-Path $PSScriptRoot "..\lib"
$MelonLoaderPath = "C:\Path\To\MIMESIS\MelonLoader\net35"
$GameManagedPath = "C:\Path\To\MIMESIS\MIMESIS_Data\Managed"

New-Item -ItemType Directory -Path (Join-Path $WorkspaceLib "melonloader") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $WorkspaceLib "game") -Force | Out-Null

Copy-Item "$MelonLoaderPath\MelonLoader.dll" -Destination (Join-Path $WorkspaceLib "melonloader\") -Force
Copy-Item "$MelonLoaderPath\0Harmony.dll" -Destination (Join-Path $WorkspaceLib "melonloader\") -Force
Copy-Item "$GameManagedPath\*.dll" -Destination (Join-Path $WorkspaceLib "game\") -Force
```

3. Run the script: `.\scripts\setup_lib.ps1`

**Option B: Manual Setup**

1. Copy `MelonLoader.dll` and `0Harmony.dll` from `MIMESIS/MelonLoader/net35/` to `Workspace/lib/melonloader/`
2. Copy all DLLs from `MIMESIS/MIMESIS_Data/Managed/` to `Workspace/lib/game/`
3. Add the `MimicAPI` folder (clone from the [MimicAPI repository](https://github.com/NeoMimicry/MimicAPI) or copy it manually)

#### 4. Commit and Push

```bash
git add lib/ MimicAPI/
git commit -m "Add game DLLs and MimicAPI"
git push origin main
```

**Important:** The `lib/` directory should be committed to the repository so that GitHub Actions can access it during builds.

### Setting Up Your Mod

1. **Clone or Copy the InventoryExpansion:**
   ```bash
   git clone https://github.com/YourUsername/InventoryExpansion.git YourModName
   cd YourModName
   ```

2. **Run the setup script:**
   ```bash
   ./setup_mod.sh
   ```
   - Enter your desired **mod name** (PascalCase, no spaces)
   - The script will:
     - Rename the project file to `YourModName.csproj`
     - Replace all occurrences of `InventoryExpansion` with `YourModName`
     - Rename the config file to `Config/YourModNamePreferences.cs`

3. **Optionally rename the folder:**
   - You can now rename the folder itself from `InventoryExpansion` to your mod name if you want it to match.

4. **Update Project Paths:**
   Edit `YourModName.csproj` and update these paths to match your setup:
   ```xml
   <ModsDirectory>C:\Path\To\MIMESIS\Mods</ModsDirectory>
   <GameExePath>C:\Path\To\MIMESIS\MIMESIS.exe</GameExePath>
   ```

5. **Configure Workspace Access:**
   The project file references the Workspace repository. Make sure the path is correct:
   ```xml
   <WorkspaceLibPath>$(MSBuildThisFileDirectory)../Workspace/lib</WorkspaceLibPath>
   ```
   
   If your Workspace is in a different location, adjust the path accordingly.

6. **Set Up GitHub Actions (Optional):**
   If you want to use GitHub Actions for automated builds and releases:
   
   **Required Secrets:**
   - `WORKSPACE_TOKEN`: SSH private key or Personal Access Token with access to your private Workspace repository
      - For SSH key: Generate with `ssh-keygen -t ed25519 -C "github-actions" -f workspace_deploy_key`
      - Add the public key as a Deploy Key in your Workspace repository
      - Use the private key content as the secret value
      - Or use a Personal Access Token (PAT) with `repo` scope
   
   **Thunderstore Upload (Optional):**
   If you want to automatically upload releases to Thunderstore:
   - `THUNDERSTORE_TOKEN`: Your Thunderstore API token
      - Get it from the Thunderstore team **Service Accounts** page:
         1. Go to https://thunderstore.io/settings/teams/
         2. Select the team you publish under (create one if needed)
         3. Open **Service Accounts** → **Add service account**
         4. Name it, click **Create**, and copy the token that starts with `tss_` (you only see it once)
      - The [upload-thunderstore-package wiki](https://github.com/GreenTF/upload-thunderstore-package/wiki) has the full illustrated guide if you need more detail
   
   **Note:** The Thunderstore package details (namespace, community, repo, etc.) are configured in the workflow file (`.github/workflows/build-and-release.yml`). Update them to match your Thunderstore package:
   - Edit the `thunderstore` job in the workflow file
   - Update `namespace`, `name`, `description`, `community`, `repo`, and other options
   - For a complete list of available options, see the [upload-thunderstore-package documentation](https://github.com/GreenTF/upload-thunderstore-package)
   
   To add secrets:
   1. Go to your repository → Settings → Secrets and variables → Actions
   2. Click "New repository secret"
   3. Add each secret with the name and value above

### Using MimicAPI (Optional)

If you want to use MimicAPI for easier game API access:

1. Uncomment the MimicAPI ProjectReference in `YourModName.csproj`:
   ```xml
   <ItemGroup>
       <ProjectReference Include="$(MimicAPIPath)/MimicAPI.csproj">
           <ReferenceOutputAssembly>true</ReferenceOutputAssembly>
           <Private>false</Private>
       </ProjectReference>
   </ItemGroup>
   ```

2. Uncomment the MimicAPI assembly attribute in `Core.cs`:
   ```csharp
   [assembly: MelonOptionalDependencies("MimicAPI")]
   ```

3. Uncomment the MimicAPI.dll copy command in the PostBuild target (optional, for auto-deployment)

4. Use `MimicAPI.GameAPI.*` namespaces in your code:
   ```csharp
   using MimicAPI.GameAPI;
   
   var player = PlayerAPI.GetLocalPlayer();
   var room = RoomAPI.GetCurrentRoom();
   ```

## Project Structure

```
YourModName/
├── Config/
│   └── YourModNamePreferences.cs    # Configuration system
├── Patches/
│   └── ExamplePatch.cs              # Harmony patches (commented out)
├── Core.cs                           # Main entry point
└── YourModName.csproj               # Project file
```

## Development

- **Core entry:** `Core.cs` - Main mod class inheriting from `MelonMod`
- **Configuration:** `Config/YourModNamePreferences.cs` - User preferences system
- **Harmony patches:** `Patches/*.cs` - Game code modifications

### Configuration

Adjustment values live in `UserData/MelonPreferences.cfg`.
Key options in `YourModName` section:
- `Enabled`: toggle the mod without removing it (default: `true`).

### Build & Deploy

**Local Build:**
The project is configured to automatically:
- Copy the built DLL to the game's Mods directory
- Optionally copy MimicAPI.dll if using MimicAPI
- Optionally auto-start the game after build (if not already running)

Update the paths in `YourModName.csproj` to match your setup:
- `ModsDirectory`: Path to MIMESIS/Mods folder
- `GameExePath`: Path to MIMESIS.exe

**Automated Releases (GitHub Actions):**
If you've set up GitHub Actions with Thunderstore secrets, you can create releases automatically:

1. Update the version in `YourModName.csproj` (e.g., `1.0.1`)
2. Commit and push the changes
3. Create a Git tag: `git tag v1.0.1`
4. Push the tag: `git push origin v1.0.1`

The GitHub Actions workflow will automatically:
- Build the project
- Verify the version
- Create a GitHub Release
- Upload the package to Thunderstore (if configured)

**Thunderstore Package:**
The `thunderstore/` folder contains:
- `manifest.json`: Package metadata (version is auto-updated during build)
- `icon.png`: Package icon
- `README.md`: Thunderstore-specific README

## Troubleshooting

### Build Errors

**Error: "Could not find file 'Assembly-CSharp.dll'"**
- Make sure your Workspace repository is set up correctly
- Verify the `WorkspaceLibPath` in your `.csproj` file points to the correct location
- Ensure the `lib/game/` directory contains all required DLLs

**Error: "The type or namespace name 'MimicAPI' could not be found"**
- Make sure you've uncommented the MimicAPI ProjectReference in your `.csproj`
- Verify the `MimicAPIPath` property points to the correct location
- Ensure MimicAPI is present in your Workspace repository

**Error: "Could not copy DLL to Mods directory"**
- Check that the `ModsDirectory` path in your `.csproj` is correct
- Ensure the Mods directory exists
- Make sure Mimesis is not running (it may lock the DLL files)

### Runtime Issues

**Mod doesn't load in game**
- Check `MelonLoader/Logs/` for error messages
- Verify the mod DLL is in the correct `Mods/` directory
- Ensure all dependencies (especially MimicAPI.dll if used) are present

**Configuration not working**
- Check `UserData/MelonPreferences.cfg` for your mod's section
- Verify the category name matches in your `Preferences.cs` file

## License
Provided as-is under the MIT License. Contributions welcome via PR.
