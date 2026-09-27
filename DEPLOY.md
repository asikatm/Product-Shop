# Online e deploy (Azure)

Ei guide follow korle software ta `https://<apnar-naam>.azurewebsites.net` link e chalu hobe.
API ar App (Blazor) **ek link e** chole — `dotnet publish` korle App er file gulo API er sathe chole jay.

Mot 4 ta kaaj: **Azure SQL database → Web App → Settings → Deploy**. 30-40 minute lagbe.

---

## 1. Azure account

- <https://azure.microsoft.com/free> e account khulun (card verify lage, free tier e taka kate na).
- Student hole <https://azure.microsoft.com/free/students> — card lage na.

## 2. Database (Azure SQL)

1. <https://portal.azure.com> → upore search e **SQL databases** → **Create**.
2. **Free offer** banner ashle **Apply offer** chapun (free database, maase limit porjonto free).
3. **Resource group**: `garments-rg` (notun).
4. **Database name**: `GarmentsShowroomDb`.
5. **Server** → **Create new**:
   - Server name: `garments-sql-<kichu-ekta>` (unique hote hobe)
   - Location: **Southeast Asia**
   - Authentication: **Use SQL authentication** → admin login ar ekta **strong password** din. **Password ta likhe rakhun.**
6. **Networking** tab → **Allow Azure services and resources to access this server: Yes**.
7. **Review + create** → **Create**.
8. Toiri hole database → **Settings → Connection strings** → **ADO.NET** er lekha ta copy korun.
   `{your_password}` er jaygay 5 number step er password boshan.

## 3. Web App

1. Search e **App Services** → **Create → Web App**.
2. Resource group: `garments-rg`.
3. **Name**: jemon `garments-showroom-dhaka` → eta-i link hobe: `https://garments-showroom-dhaka.azurewebsites.net`.
4. Publish: **Code** · Runtime stack: **.NET 9 (STS)** · Operating system: **Linux** · Region: **Southeast Asia**.
5. Pricing plan: **Free F1** (test er jonno) ba **Basic B1** (shop e niyomito use er jonno — druto, sleep kore na).
6. **Review + create** → **Create**.

## 4. Settings (password / key)

Web App → **Settings → Environment variables**:

**App settings** tab e **+ Add**:

| Name | Value |
|---|---|
| `Jwt__Key` | notun random key (nicher command diye banan) |
| `UploadsPath` | `/home/data/uploads` |

**Connection strings** tab e **+ Add**:

| Name | Value | Type |
|---|---|---|
| `DefaultConnection` | 2.8 e copy kora connection string (password shoho) | **SQLAzure** |

Random key banate PowerShell e:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

**Apply** → **Confirm** chapun.

> `UploadsPath` dile product er chobi app folder er baire thake, tai notun deploy e chobi muche jay na.
> `appsettings.json` er local SQL password / JWT key online e use hoy na — upore deya value gulo oigulo override kore.

## 5. Deploy — duita upay, jekono ekta

### Upay A: GitHub theke automatic (recommended)

Ekbar setup korle GitHub e `main` e push korlei online version update hoye jabe.

1. Web App → **Settings → Configuration → General settings** → **SCM Basic Auth Publishing Credentials: On** → Save.
2. Web App → **Overview** → **Download publish profile** (ekta `.PublishSettings` file namabe).
3. GitHub e repo → **Settings → Secrets and variables → Actions**:
   - **Secrets** tab → **New repository secret** → Name: `AZURE_WEBAPP_PUBLISH_PROFILE`, Value: download kora file ta Notepad e khule **puro lekha** paste.
   - **Variables** tab → **New repository variable** → Name: `AZURE_WEBAPP_NAME`, Value: 3.3 er naam (jemon `garments-showroom-dhaka`).
4. Repo → **Actions** tab → **Deploy to Azure** → **Run workflow**. Shobuj ✓ hole deploy shesh (3-5 minute).

Workflow ta ache `.github/workflows/deploy-azure.yml` e.

### Upay B: Visual Studio theke

1. **ProductShop.Api.sln** khulun → Solution Explorer e **ProductShop.Api** te right-click → **Publish**.
2. **Azure → Azure App Service (Linux)** → Microsoft account diye login → 3 number step er Web App select → **Finish**.
3. **Publish** chapun.

(VS Publish Release mode e hoy, tai App o sathe chole jay.)

## 6. Login

1. `https://<apnar-naam>.azurewebsites.net` khulun.
2. Prothom bar ektu shomoy nite pare — database table ar default admin nije toiri hoy.
3. **admin / admin123** diye login korun → **shathe shathe** upore dan e naam → **Password change** e giye password bodlan.
4. Onnora **Notun account khulun** diye request pathabe → **Users** page theke approve korun.

---

## Jana dorkar

- **Data**: Online database ta notun, apnar PC er data automatic jabe na. Product / customer notun kore dite hobe
  (ba SQL Server Management Studio → database e right-click → **Tasks → Deploy Database to Microsoft Azure SQL Database**).
- **Free F1**: kichukkhon use na hole app "ghumay", prothom request e 20-30 second lage. Din e 60 minute CPU limit.
  Shop e shara din use korle **B1** nin.
- **Free SQL**: use na hole auto-pause hoy; prothom request e kichu second deri hote pare.

## Problem hole

| Dekhchen | Ki korben |
|---|---|
| `HTTP Error 500.30` / site khulche na | Web App → **Monitoring → Log stream** e error dekhun. Beshir bhag shomoy connection string vul ba password bosano hoy nai. |
| "Cannot open server ... firewall" | SQL server → **Networking** → *Allow Azure services* **Yes** ache kina dekhun. |
| Login e "Login er meyad shesh" barbar | `Jwt__Key` setting ta deya ache kina dekhun (deploy er por key bodlale sobaike abar login korte hobe — eta thik ache). |
| GitHub Actions e "Deploy skipped" | `AZURE_WEBAPP_PUBLISH_PROFILE` secret ta deya hoy nai. |

## VPS / nijer server (Azure chara)

Windows VPS hole:
1. [.NET 9 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0) ar SQL Server Express install.
2. PC te: `dotnet publish Api/ProductShop.Api/ProductShop.Api.csproj -c Release -o publish`
3. `publish` folder ta server e copy → IIS e notun site (ba `ProductShop.Api.exe --urls http://0.0.0.0:80`).
4. Server e environment variable: `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `UploadsPath`.
