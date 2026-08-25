# অন্য PC-তে Nova Agent setup ও coding guide

এই guide-এ দুইটি আলাদা workflow আছে:

1. অন্য PC-তে source code নিয়ে development/coding করা
2. code edit না করে শুধু Nova Agent চালানো

## A. অন্য PC-তে coding/development

### 1. Source transfer

Project folder ZIP করে বা Git repository দিয়ে নতুন PC-তে নিন। `bin`, `obj`, `.tools`, এবং `publish` folder transfer করার প্রয়োজন নেই; এগুলো আবার তৈরি হবে। Whisper model সঙ্গে নিতে চাইলে `src\NovaAgent\runtime\whisper` folder রাখতে পারেন—তাহলে বড় model আবার download করতে হবে না।

### 2. প্রয়োজনীয় software

নতুন Windows 10/11 x64 PC-তে install করুন:

- .NET 10 SDK
- Visual Studio: `.NET desktop development`

PC restart বা অন্তত নতুন PowerShell window খোলার পরে project root-এ যাচাই করুন:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\check-prerequisites.ps1
```

### 3. Whisper runtime তৈরি

Runtime/model transfer না করলে চালান:

```powershell
.\scripts\setup-whisper.ps1 -Model base
```

Model choice:

- `tiny`: সবচেয়ে দ্রুত, accuracy কম
- `base`: সাধারণ PC-র জন্য recommended balance
- `small`: accuracy ভালো, RAM/CPU usage বেশি

Script pinned official prebuilt `whisper.cpp` Windows x64 runtime, প্রয়োজনীয় DLL এবং multilingual model download করে। তাই CMake বা C++ build tools প্রয়োজন হয় না।

### 4. Build এবং run

Command line:

```powershell
.\scripts\build.ps1
dotnet run --project .\src\NovaAgent\NovaAgent.csproj
```

Visual Studio:

1. `NovaAgent.sln` open করুন।
2. Configuration `Release`, platform `Any CPU` রাখুন।
3. `NovaAgent` startup project করুন।
4. `F5` বা `Ctrl+F5` চাপুন।

`global.json` compatible .NET 10 SDK select করে। Restore error হলে প্রথমে `dotnet --list-sdks` দিয়ে 10.x SDK আছে কি না দেখুন।

### 5. এক command-এ complete setup ও publish

```powershell
.\scripts\setup-and-publish.ps1 -Model base
```

Output: `publish\win-x64\NovaAgent.exe`

### 6. নিজের settings migrate

পুরোনো PC-তে Nova খুলে **Settings → Export settings** করুন। JSON file নতুন PC-তে এনে **Import settings** করুন। তারপর:

- Microphone আবার select করুন
- absolute Whisper/app path review করুন
- **Save settings** চাপুন
- Diagnostics tab-এ **Run checks** দিন

Username/drive letter আলাদা হলে absolute custom app path বদলাতে হবে। Relative Whisper paths portable থাকে।

## B. শুধু চালানোর জন্য অন্য PC-তে নেওয়া

Build PC-তে runtime setup শেষ হওয়ার পরে:

```powershell
.\scripts\package-portable.ps1
```

তারপর:

1. `publish\NovaAgent-win-x64-portable.zip` অন্য PC-তে copy করুন।
2. ZIP সম্পূর্ণ extract করুন; ZIP-এর ভিতর থেকে সরাসরি চালাবেন না।
3. `NovaAgent.exe` run করুন।
4. Diagnostics tab-এ সব check দেখুন।
5. প্রয়োজন হলে microphone select করে Save দিন।

Package self-contained, তাই target PC-তে আলাদা .NET runtime লাগে না। Whisper native binary start না হলে Microsoft Visual C++ 2015–2022 Redistributable x64 install করুন। Windows SmartScreen unsigned application warning দেখাতে পারে; public distribution-এর আগে code signing recommended।

ইচ্ছা করলে target PC-তেই current user-এর জন্য install/shortcut তৈরি করা যায়:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\install-local.ps1 -PublishedFolder "D:\Path\To\Extracted\NovaAgent"
```

## Common সমস্যা ও সমাধান

### `whisper-server.exe` বা model missing

- Development PC: `.\scripts\setup-whisper.ps1` আবার চালান।
- Portable PC: ZIP-এ `runtime\whisper\whisper-server.exe` এবং `ggml-base.bin` আছে কি না দেখুন।

### Microphone পাওয়া যাচ্ছে না

- Windows Settings → Privacy & security → Microphone-এ desktop app access allow করুন।
- Nova Settings-এ correct device select করুন।
- Bluetooth headset connect করার পরে Nova restart করুন।

### Voice slow বা CPU বেশি

- `tiny`/`base` model ব্যবহার করুন।
- Always listening off করে **Listen once** ব্যবহার করুন।
- Audio chunk 5000–6500 ms করে পরীক্ষা করুন।

### Build restore error

```powershell
dotnet --list-sdks
dotnet restore .\NovaAgent.sln
dotnet build .\NovaAgent.sln -c Release
```

Corporate proxy/firewall থাকলে NuGet, GitHub, এবং model download access প্রয়োজন হবে।

### Log কোথায়

Diagnostics → **Open log**, অথবা:

```text
%LOCALAPPDATA%\NovaAgent\Logs\
```

Error report করার সময় latest log, Windows version, selected model, এবং exact command দিন; ব্যক্তিগত command history share করার আগে review করুন।
