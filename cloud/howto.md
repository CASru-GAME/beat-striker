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
  cloudresourcemanager.googleapis.com \
  run.googleapis.com \
  cloudbuild.googleapis.com \
  firestore.googleapis.com \
  iam.googleapis.com \
  iamcredentials.googleapis.com \
  sts.googleapis.com \
  artifactregistry.googleapis.com
```

> **注意**: `cloudresourcemanager.googleapis.com` は Terraform の Google プロバイダーが
> Project Service の管理や IAM ポリシーの読み書きに使用するため、必ず最初に有効化すること。

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
  --role="roles/artifactregistry.admin"

# --- Terraform apply に必要な追加権限 ---

# API有効化 (google_project_service)
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/serviceusage.serviceUsageAdmin"

# サービスアカウント作成 (google_service_account)
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/iam.serviceAccountAdmin"

# IAMバインディング設定 (google_project_iam_member)
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/resourcemanager.projectIamAdmin"
```

---

## 6. Terraform state 用 GCS バケット作成

Terraform のリモートバックエンド（`backend "gcs"`）用のバケットを作成する。  
このバケットは Terraform 管理外で事前に用意する必要がある。

```bash
gsutil mb -p "$PROJECT_ID" -l asia-northeast1 gs://tf-state-beat-495400

# バージョニングを有効化（state の破損防止）
gsutil versioning set on gs://tf-state-beat-495400
```

### GitHub Actions SA に state バケットへのアクセス権限を付与

```bash
gsutil iam ch \
  "serviceAccount:${SA_EMAIL}:roles/storage.objectAdmin" \
  gs://tf-state-beat-495400
```

---

## 7. プロジェクト番号取得

```bash
export PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')"
echo $PROJECT_NUMBER
```

---

## 8. Workload Identity Pool 作成

```bash
gcloud iam workload-identity-pools create "$POOL_ID" \
  --project="$PROJECT_ID" \
  --location="global" \
  --display-name="GitHub Pool"
```

---

## 9. Provider 作成

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

## 10. GitHubリポジトリのアクセス許可

```bash
gcloud iam service-accounts add-iam-policy-binding "$SA_EMAIL" \
  --project="$PROJECT_ID" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL_ID}/attribute.repository/${OWNER}/${REPO}"
```

---

## 11. Cloud Build サービスアカウントへの権限付与

`gcloud run deploy --source` は内部的に Cloud Build を使用してコンテナイメージをビルドする。  
Cloud Build のデフォルト SA に Artifact Registry への書き込み権限が必要。

```bash
# Cloud Build デフォルト SA
export CLOUDBUILD_SA="${PROJECT_NUMBER}@cloudbuild.gserviceaccount.com"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:${CLOUDBUILD_SA}" \
  --role="roles/artifactregistry.writer"
```

---

## 12. GitHubに設定する値

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
* `terraform apply` がエラーなく完了する
* Cloud Run へデプロイ可能

---

## 補足

今回の設定で実現していること：

* GitHub Actions → GCP の安全な認証（OIDC）
* サービスアカウントのなりすまし許可
* 特定リポジトリのみアクセス許可
* Terraform によるインフラ管理（API有効化・SA作成・IAMバインディング）

---

## トラブルシューティング

### `Cloud Resource Manager API has not been used` エラー

Terraform が Project Service や IAM を操作する際に `cloudresourcemanager.googleapis.com` が  
必要。手順2で有効化されていることを確認する。

```bash
gcloud services list --enabled --filter="name:cloudresourcemanager.googleapis.com"
```

### Terraform state のロック/アクセスエラー

GCS バケット `tf-state-beat-495400` が存在し、SA に適切な権限があるか確認する。

```bash
gsutil ls gs://tf-state-beat-495400
gsutil iam get gs://tf-state-beat-495400
```

---

## 次のステップ

* deploy.yml 作成
* Cloud Run デプロイ自動化
* Firestoreへの書き込みAPI実装
