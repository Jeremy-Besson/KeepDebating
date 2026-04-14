# Azure Deployment Checklist

Complete these steps to deploy TryingStuff to Azure.

## Prerequisites

- [ ] Azure account with active subscription
- [ ] Azure CLI installed (`az --version`)
- [ ] GitHub repository with Admin access
- [ ] Azure OpenAI resource (endpoint + API key)

---

## 1. Azure Setup

### Create Resource Group

```powershell
az login
az group create --name tryingstuff-rg --location eastus
```

- [ ] Resource group `tryingstuff-rg` created

### Get Subscription ID

```powershell
az account show --query id -o tsv
```

Store this value; you'll need it multiple times.

- [ ] Subscription ID noted: `__________________________________`

---

## 2. Service Principal & GitHub Authentication

### Create Service Principal (Option A: Traditional)

```powershell
az ad sp create-for-rbac `
  --name tryingstuff-github `
  --role Contributor `
  --scopes /subscriptions/<YOUR_SUBSCRIPTION_ID>/resourceGroups/tryingstuff-rg
```

Copy the output JSON values:

- [ ] `AZURE_CLIENT_ID` (appId):  `__________________________________`
- [ ] `AZURE_TENANT_ID` (tenant): `__________________________________`
- [ ] `AZURE_SUBSCRIPTION_ID`: `__________________________________`

**Note:** Store `clientSecret` securely (used only once).

### Set Up OIDC (Option B: Recommended, Keyless)

If using OIDC, follow Azure's federated identity setup:
- [ ] OIDC federated identity configured in Azure AD

---

## 3. Provision Azure Infrastructure

From repository root:

```powershell
cd c:\Users\JeremyBesson\source\repos\AITrainingMSDT\TryingStuff

az deployment group create `
  --resource-group tryingstuff-rg `
  --template-file infra/main.bicep `
  --parameters infra/parameters/dev.bicepparam
```

Wait for deployment to complete. Bicep will create:

- [ ] App Service Plan
- [ ] App Service Web App (`tryingstuff-api-dev`)
- [ ] Static Web App (`tryingstuff-web-dev`)
- [ ] Key Vault
- [ ] Application Insights
- [ ] Log Analytics Workspace

### Capture Deployment Outputs

```powershell
az deployment group show --resource-group tryingstuff-rg --name main --query properties.outputs -o json
```

Notes:

- [ ] Web App Host: `__________________________________`
- [ ] Static Web App Default Host: `__________________________________`
- [ ] Key Vault Name: `__________________________________`

---

## 4. Configure Backend Secrets (App Service)

Navigate to Azure Portal > App Service > `tryingstuff-api-dev` > Configuration > Application settings

Add new app settings:

- [ ] `AzureOpenAI__Endpoint` = `__________________________________`
- [ ] `AzureOpenAI__ApiKey` = `__________________________________`
- [ ] `AzureOpenAI__Model` = `gpt-4.1-mini` (or your model name)

Click **Save**.

- [ ] All app settings saved and restarted

---

## 5. Update Frontend API Route

Edit [frontend/public/staticwebapp.config.json](frontend/public/staticwebapp.config.json):

Replace the placeholder in line 2:

```json
"rewrite": "https://REPLACE_WITH_BACKEND_HOST/api/*"
```

with your App Service host (from step 3):

```json
"rewrite": "https://tryingstuff-api-dev.azurewebsites.net/api/*"
```

- [ ] `staticwebapp.config.json` updated with backend URL

---

## 6. Get Static Web Apps Deployment Token

Navigate to Azure Portal > Static Web Apps > `tryingstuff-web-dev` > Manage deployment token

Copy the token.

- [ ] SWA deployment token copied: `__________________________________`

---

## 7. Add GitHub Repository Secrets

In GitHub > Your Repository > Settings > Secrets and variables > Actions > New repository secret

Add these secrets one by one:

```
AZURE_CLIENT_ID = <from step 2>
AZURE_TENANT_ID = <from step 2>
AZURE_SUBSCRIPTION_ID = <from step 2>
AZURE_WEBAPP_NAME = tryingstuff-api-dev
AZURE_STATIC_WEB_APPS_API_TOKEN = <from step 6>
```

- [ ] `AZURE_CLIENT_ID` added
- [ ] `AZURE_TENANT_ID` added
- [ ] `AZURE_SUBSCRIPTION_ID` added
- [ ] `AZURE_WEBAPP_NAME` added
- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN` added

---

## 8. Commit & Push Code

```powershell
git add .
git commit -m "feat(infra): add azure bicep templates and deploy workflows"
git push origin main
```

- [ ] Code pushed to main branch

Verify workflows appear in GitHub > Actions:
- [ ] Backend CI workflow visible
- [ ] Frontend CI workflow visible
- [ ] Deploy BackEnd workflow visible
- [ ] Deploy FrontEnd workflow visible

---

## 9. Trigger Initial Deployment

In GitHub > Actions:

1. **Deploy BackEnd**
   - [ ] Run workflow manually
   - [ ] Wait for completion
   - [ ] Verify no errors

2. **Deploy FrontEnd**
   - [ ] Run workflow manually
   - [ ] Wait for completion
   - [ ] Verify no errors

---

## 10. Verify Deployment

### Test Backend Health

```powershell
$backendUrl = "https://tryingstuff-api-dev.azurewebsites.net/api/health"
Invoke-WebRequest -Uri $backendUrl
```

- [ ] Backend responds with `{ "status": "ok", "service": "Debaters BackEnd" }`

### Test Frontend

Open in browser:
```
https://tryingstuff-web-dev.azurestaticapps.net
```

- [ ] Frontend loads without errors
- [ ] Can see debate interface
- [ ] Can list debaters (calls `/api/debaters`)

### Test Full Flow

1. Select debaters and topic
2. Start debate
3. Monitor trace in Application Insights

- [ ] Debate runs successfully
- [ ] SSE stream works (live updates)
- [ ] Application Insights shows traces/requests

---

## 11. Monitor & Troubleshoot

### View Logs

Backend:
```powershell
az webapp log tail --resource-group tryingstuff-rg --name tryingstuff-api-dev
```

### Check Application Insights

Azure Portal > Application Insights > `tryingstuff-appi-dev` > Live Metrics

- [ ] Requests flowing through
- [ ] No persistent errors

---

## 12. Ongoing Operations

After initial setup, deployment is automated:

1. **Push code to main** → CI builds run (backend-ci.yml, frontend-ci.yml)
2. **Manually trigger deploy** → GitHub Actions > Deploy BackEnd / Deploy FrontEnd

To re-deploy after code changes:

```powershell
git add .
git commit -m "your message"
git push origin main
# Then manually run deploy workflow in GitHub Actions
```

- [ ] Deployment process understood and documented

---

## Troubleshooting

### Deploy workflow fails with "Invalid Azure credentials"

- Check GitHub secrets are set correctly
- Verify service principal has Contributor role on resource group
- Regenerate credentials if needed

### Backend app not responding

- Check app settings in Azure Portal (AzureOpenAI__* values)
- View logs: `az webapp log tail --resource-group tryingstuff-rg --name tryingstuff-api-dev`
- Restart app service: `az webapp restart --resource-group tryingstuff-rg --name tryingstuff-api-dev`

### Frontend shows "Failed to connect to /api"

- Verify `staticwebapp.config.json` has correct backend URL
- Confirm backend is deployed and running
- Check SWA routing rules in Azure Portal

### Can't call /api from frontend (CORS error)

- Backend CORS is currently restricted to dev environment
- For production, update [backend/Program.cs](backend/Program.cs#L8) CORS policy with SWA domain

---

## Notes

- Record all resource names and IDs for future reference
- Keep GitHub secrets secure; rotate credentials periodically
- Monitor Azure costs (App Service, Static Web Apps are low-cost for dev; scale as needed)
