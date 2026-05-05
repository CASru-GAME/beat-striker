# GCP + GitHub Actions セットアップ手順（Cloud Run デプロイ用）

## 概要

GitHub Actions から Google Cloud に安全にデプロイするために  
Workload Identity Federation（鍵なし認証）を設定する。

---

## 前提

- GCPプロジェクト作成済み
- Cloud Shell または gcloud CLI 使用可能
- GitHubリポジトリ作成済み

---

## 使用情報

```bash
PROJECT_ID=beat-495400
OWNER=CASru-GAME
REPO=beat-striker

SA_NAME=github-actions
POOL_ID=github-pool
PROVIDER_ID=github-provider
````

---

## 1. プロジェクト設定

```bash
gcloud config set project beat-495400
```

---

## 2. 必要APIの有効化

```bash
gcloud services enable \
  run.googleapis.com \
  cloudbuild.googleapis.com \
  firestore.googleapis.com \
  iam.googleapis.com \
  iamcredentials.googleapis.com \
  sts.googleapis.com \
  artifactregistry.googleapis.com
```

---

## 3. サービスアカウント作成

```bash
gcloud iam service-accounts create github-actions \
  --display-name="GitHub Actions"
```

---

## 4. 環境変数設定

```bash
export PROJECT_ID="beat-495400"
export OWNER="CASru-GAME"
export REPO="beat-striker"

export SA_NAME="github-actions"
export POOL_ID="github-pool"
export PROVIDER_ID="github-provider"

export SA_EMAIL="${SA_NAME}@${PROJECT_ID}.iam.gserviceaccount.com"
```

---

## 5. 権限付与

```bash
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/run.admin"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/cloudbuild.builds.editor"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/datastore.owner"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/iam.serviceAccountUser"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/artifactregistry.writer"
```

---

## 6. プロジェクト番号取得

```bash
export PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')"
echo $PROJECT_NUMBER
```

---

## 7. Workload Identity Pool 作成

```bash
gcloud iam workload-identity-pools create "$POOL_ID" \
  --project="$PROJECT_ID" \
  --location="global" \
  --display-name="GitHub Pool"
```

---

## 8. Provider 作成

```bash
gcloud iam workload-identity-pools providers create-oidc "$PROVIDER_ID" \
  --project="$PROJECT_ID" \
  --location="global" \
  --workload-identity-pool="$POOL_ID" \
  --display-name="GitHub Provider" \
  --issuer-uri="https://token.actions.githubusercontent.com" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository" \
  --attribute-condition="assertion.repository == 'CASru-GAME/beat-striker'"
```

---

## 9. GitHubリポジトリのアクセス許可

```bash
gcloud iam service-accounts add-iam-policy-binding "$SA_EMAIL" \
  --project="$PROJECT_ID" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL_ID}/attribute.repository/${OWNER}/${REPO}"
```

---

## 10. GitHubに設定する値

以下を GitHub の

```
Settings → Secrets and variables → Actions → Variables
```

に登録する

---

### GCP_PROJECT_ID

```
beat-495400
```

---

### GCP_SERVICE_ACCOUNT

```
github-actions@beat-495400.iam.gserviceaccount.com
```

---

### GCP_WIF_PROVIDER

```
projects/1049753443537/locations/global/workloadIdentityPools/github-pool/providers/github-provider
```

---

## 完了条件

以下ができれば成功

* GitHub Actions から GCP に認証できる
* JSONキー不要
* Cloud Run へデプロイ可能

---

## 補足

今回の設定で実現していること：

* GitHub Actions → GCP の安全な認証（OIDC）
* サービスアカウントのなりすまし許可
* 特定リポジトリのみアクセス許可

---

## 次のステップ

* deploy.yml 作成
* Cloud Run デプロイ自動化
* Firestoreへの書き込みAPI実装
