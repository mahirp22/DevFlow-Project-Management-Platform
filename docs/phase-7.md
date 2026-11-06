# Phase 7: private S3 task attachments

Phases 5 and 6 were committed together as `ec1f54f` after the user confirmed both live walkthroughs. Phase 7 adds file upload, verification, download and abandoned-upload cleanup. The AWS bucket and local login still need to be configured by you; no AWS resources or access keys were created by the agent. Phase 7 is not committed. Phase 8 is not authorized.

## AWS setup: follow these steps in order

### 1. Create the private bucket

1. Sign into the AWS console using the same account you used for Cognito.
2. Use the search box at the top to search for **S3**, then open **S3**.
3. Open **General purpose buckets** and select **Create bucket**.
4. Set **AWS Region** to **Canada (Central) — ca-central-1**.
5. Keep the bucket type as **General purpose** if that option is shown.
6. Enter a globally unique **Bucket name**, such as `devflow-attachments-yourname-2026`. Replace `yourname` with your own identifier. Use lowercase letters, numbers and hyphens; do not use dots. If AWS says the name is taken, add a few random digits.
7. Under **Object Ownership**, keep **ACLs disabled (recommended)** / **Bucket owner enforced**.
8. Keep **Block all public access** checked. Do not make the bucket public.
9. Under **Bucket Versioning**, keep **Disable**. Use a new bucket that has never had versioning enabled. This milestone's cleanup deliberately requires an unversioned bucket and rejects Enabled or Suspended versioning.
10. Under **Default encryption**, select **Server-side encryption with Amazon S3 managed keys (SSE-S3)**. This is the AES256 option; do not choose KMS for this setup.
11. Leave Object Lock disabled and do not enable static website hosting.
12. Select **Create bucket**. Write down the exact bucket name for the remaining steps.

S3 stores uploaded data and charges for storage/requests according to your AWS plan. Test with small non-sensitive files.

### 2. Allow the local frontend to upload

1. Open your new bucket by clicking its name.
2. Select the **Permissions** tab.
3. Scroll to **Cross-origin resource sharing (CORS)** and select **Edit**.
4. In VS Code, open `infrastructure/aws/attachments-cors.json`.
5. Copy its entire contents into the CORS editor and select **Save changes**.

The allowed frontend origin is exactly `http://localhost:5173`. Use that address in the browser, not 127.0.0.1. CORS does not make the bucket public; uploads still require a signed policy from the API.

### 3. Require HTTPS for bucket access

1. Stay on the bucket's **Permissions** tab.
2. Find **Bucket policy** and select **Edit**.
3. Open `infrastructure/aws/attachments-bucket-policy.json` in VS Code and copy the whole file into the AWS editor.
4. In the AWS editor, replace **both** occurrences of `REPLACE_WITH_BUCKET_NAME` with your exact bucket name. Keep the ARN prefixes and trailing `/*` intact.
5. Select **Save changes**.

This policy denies unencrypted transport; it does not grant public access. Leave Block all public access enabled.

### 4. Give your development identity S3 permission

Use your IAM development user/role rather than root for daily development. The identity you log into with the AWS CLI must have the following policies. If your AWS administrator manages permissions, ask them to attach these policies to your development identity.

For an IAM user you manage yourself:

1. Search for **IAM** in the AWS console and open it.
2. In the left navigation, choose **Policies**, then **Create policy**.
3. Select the **JSON** editor.
4. Open `infrastructure/aws/attachments-iam-policy.json` in VS Code and copy its full contents into the editor.
5. Replace **both** `REPLACE_WITH_BUCKET_NAME` placeholders with your actual bucket name.
6. Select **Next**. Name the policy `DevFlowAttachmentStorage`. Select **Create policy**.
7. In IAM's left navigation, choose **Users**, then your development IAM username.
8. Select **Permissions → Add permissions → Add permissions**.
9. Choose **Attach policies directly**. Find and select `DevFlowAttachmentStorage`.
10. Also find and select the AWS-managed policy **SignInLocalDevelopmentAccess**. It permits the temporary browser-based local login used in the next step.
11. Select **Next**, review the selection and choose **Add permissions**.

The S3 policy grants object operations only under `attachments/` in the one bucket, plus bucket inspection/listing. It contains no keys. Cognito users do not receive this IAM policy; the backend's AWS identity signs their authorized uploads/downloads.

### 5. Install AWS CLI version 2

1. Open the official [AWS CLI installation guide](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html).
2. Expand **Windows** and download/run the linked 64-bit MSI installer.
3. Complete the installation wizard.
4. Close and reopen VS Code so new terminals inherit the updated PATH. You will need to restart your API/frontend afterward.
5. Open **Terminal → New Terminal** and run:

```powershell
aws --version
```

You need AWS CLI **2.32.0 or newer** for the login command below. If `aws` is not recognized, restart VS Code again. The usual installation path is `C:\Program Files\Amazon\AWSCLIV2\aws.exe`.

### 6. Sign into AWS locally without creating access keys

Run:

```powershell
aws login --profile devflow
```

1. If asked for a region, enter `ca-central-1`.
2. Open the browser link if it does not open automatically.
3. Choose/sign into the same AWS development identity given permission in Step 4, then complete the displayed authorization flow.
4. Return to the terminal after it reports success.
5. Verify the profile with:

```powershell
aws sts get-caller-identity --profile devflow
```

Expected: an account/identity response. Do not paste credential files or tokens into chat. The app includes the AWS Signin SDK support needed to use this login profile. If you use IAM Identity Center instead, use your organization's SSO setup; do not substitute an IAM-user login flow without checking your identity type.

### 7. Configure and start the backend

Stop any existing DevFlow backend with **Ctrl+C** in its terminal first. Keep PostgreSQL running. In that backend terminal, paste the block below. Replace `YOUR_BUCKET_NAME_HERE` with your real bucket name before running it:

```powershell
Set-Location "C:\Users\conta\Documents\Codex\2026-11-01\i-want-to-build-a-professional\outputs\devflow"

$env:DOTNET_ROOT = (Resolve-Path "../../work/dotnet").Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME = [System.IO.Path]::GetFullPath("$PWD/../../work/dotnet-home")
$env:NUGET_PACKAGES = [System.IO.Path]::GetFullPath("$PWD/../../work/nuget-packages")
$env:ConnectionStrings__DevFlow = (Get-Content "../../work/postgres-connection.txt" -Raw).Trim()

$env:AWS_PROFILE = 'devflow'
$env:AWS_REGION = 'ca-central-1'
$env:Attachments__Region = 'ca-central-1'
$env:Attachments__BucketName = 'YOUR_BUCKET_NAME_HERE'

dotnet build backend/DevFlow.slnx --no-restore --configuration Debug
```

After **Build succeeded**, run these commands one at a time:

```powershell
dotnet ef database update --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --configuration Debug --no-build
```

```powershell
dotnet run --project backend/src/DevFlow.Api --launch-profile http --configuration Debug --no-build
```

This phase uses Debug in the walkthrough because the previously running Phase 6 backend used Release. Both configurations contain the same source; using a separate output directory let us build without interrupting your API. Keep the backend terminal open after **Now listening on …5080**. Keep your existing Cognito User Secrets/settings; no Cognito configuration changes are required. Do not change APPDATA. These environment variables last only for this terminal.

If you want the bucket/region settings to persist between terminals, you can later save `Attachments:BucketName` and `Attachments:Region` using .NET User Secrets. AWS credentials remain in the AWS CLI login profile and never belong in frontend `.env.local`.

### 8. Start the frontend

In a separate VS Code terminal:

```powershell
Set-Location "C:\Users\conta\Documents\Codex\2026-11-01\i-want-to-build-a-professional\outputs\devflow\frontend"
npm.cmd run dev
```

If Vite is already running, use that instance or stop it before starting another. Open **http://localhost:5173** and sign in.

### 9. Test a real upload and download

1. Open Windows Notepad and type `Hello from DevFlow attachments.`
2. Save it as `devflow-test.txt` somewhere you can find it, such as Documents.
3. In DevFlow, open your project, then open a task.
4. Scroll to **Attachments** below Comments/Activity.
5. Click **Choose attachment**, select `devflow-test.txt`, then click **Upload attachment**.
6. Wait for **File attached.** The stages are preparing, uploading and verifying.
7. Confirm the filename, size and uploader appear in the list.
8. Click **Download**. Confirm the downloaded file contains the text you saved.
9. In your second account's browser window, open the same task. Confirm the finalized file appears and downloads for that project member.
10. Set the second account to Viewer and confirm it can download existing files but has no upload form.
11. In AWS S3, open your bucket's **Objects** tab. The object should be stored under a generated `attachments/...` key, without a public ACL. An unsigned object link should not grant access.

If upload succeeds but the final API call is interrupted, **Finish upload** retries verification for the same record. An upload permission lasts five minutes and the database pending record expires after fifteen minutes. If the file never reached S3, select it again for a fresh upload. Expired pending entries disappear; cleanup removes their stored bytes later.

### Troubleshooting

| Symptom | Check |
| --- | --- |
| File uploads are not configured | Both Attachments environment variables must be set in the terminal that starts the API; restart it |
| Storage unavailable (503) | Run `aws login --profile devflow` again; check AWS_PROFILE, bucket/region, IAM permissions, SSE-S3 and disabled versioning |
| Browser upload fails / CORS error | Bucket CORS must allow `http://localhost:5173`; confirm you used localhost and saved the full JSON array |
| 403 from S3 | Confirm PutObject permission on the exact bucket/prefix and a valid local login; temporary credentials may have expired |
| File has not reached S3 | Finish upload cannot resend bytes; select the file and upload again |
| File does not match approved metadata | Do not change signed form fields; choose the file again; only the original approved bytes can finalize |
| 409 quota | Each task allows 20 finalized or unexpired pending uploads; expired pending entries release slots after 15 minutes |
| Port 5080/5173 occupied | Stop the older API/Vite process before starting a replacement |

## Design and deliberate limits

The API authorizes the workspace/project/task before generating a five-minute presigned POST. Its signed policy fixes the generated object key, exact byte length, content type, SHA-256 checksum, encryption and download disposition. S3 verifies the checksum during upload. Finalization independently checks S3 metadata before marking the record ready and adding task activity atomically. Only the original uploader can finalize; only current project readers can request a download. Short-lived download grants expire after 60 seconds and force attachment/octet-stream responses. Existing grants remain usable until expiration even if permissions are revoked during that minute.

The browser sends file bytes directly to S3 without a Cognito Authorization header or cookies. Only metadata goes through the REST API. The database stores object keys and metadata, not signed URLs. Never log or persist presigned credentials. TLS and private bucket access remain required.

Limits: 10 MiB per file, 20 active attachments/pending uploads per task, PDF/PNG/JPEG/TXT only. Extension and declared type validation are not content inspection or malware scanning. Files are always downloaded, not rendered inline; antivirus/quarantine is not implemented. No multipart uploads, attachment removal UI, previews, or retention purge for finalized records is included. Task/project soft deletion blocks new download grants but preserves existing attachment records and objects.

For this milestone, use a dedicated **unversioned** bucket. The storage adapter rejects both Enabled and Suspended versioning so cleanup cannot silently leave hidden object versions behind. Replaying an unexpired signed upload can upload only the same approved bytes/type, because the checksum and metadata are fixed by the policy. It cannot replace a finalized file with different content. Do not allow unrelated writers to modify these objects outside this application.

An hourly worker processes at most 100 pending records whose expiry is over a day old. It deletes their S3 objects and retains database tombstones, revisiting them daily to catch delayed/replayed uploads. Finalized files are excluded. SKIP LOCKED coordinates concurrent cleanup workers. Failed deletion remains retryable; no database record is marked cleaned until the transaction succeeds. The API must be running for cleanup; Phase 8 deployment must keep this worker active or schedule an equivalent job. No blanket S3 expiration rule is applied to the attachments prefix because it also contains finalized files.

Uploads/downloads fail safely when storage is unconfigured, while all earlier features remain usable. Default AWS credential resolution supports the local login profile now and ECS task-role credentials later. Only public bucket/region configuration is added to app settings; credentials are never hardcoded.

## Files and validation

New backend files: Domain TaskAttachment; Application/Attachments contracts; Infrastructure/Attachments service and S3 adapter; persistence AttachmentConfiguration and TaskAttachments migration; API AttachmentsController and AttachmentCleanup; AttachmentTests and S3StorageTests. Modified: DbContext, DI, Program, exception handler, appsettings, Infrastructure project/package lockfiles and test factory.

New frontend files: `AttachmentPanel.tsx`, `attachments.ts`, `attachments.test.tsx`. Modified TaskPage to display the panel. Infrastructure templates are under `infrastructure/aws/attachments-*.json`. Documentation: this guide, README, roadmap, architecture and data model.

The AWS SDK packages were verified against official NuGet metadata before installation: AWSSDK.S3 4.0.104.10, AWSSDK.Signin 4.0.102.2, transitive AWSSDK.Core 4.0.102.18. These support the existing .NET 10 application. No frontend dependency was added. The sandbox's .NET transport could not be used for package downloads, so official packages were fetched using Node's system CA trust and restored from a workspace feed. NuGet audit was unavailable for that feed; normal repository NuGet sources are unchanged.

Run backend tests using the Phase 2 Docker/external-PostgreSQL setup and `dotnet test --solution backend/DevFlow.slnx --configuration Debug --no-build --no-restore`. Frontend checks are `npm.cmd test`, `npm.cmd run lint`, and `npm.cmd run build` from frontend.

Local tests use isolated PostgreSQL databases and a test-only storage adapter for HTTP workflows. Adapter tests use the real AWS SDK signing implementation with fake test credentials and an overridden bucket-inspection call; they make no AWS calls. Live S3 CORS, IAM/login and checksum behavior must still be verified through Step 9 before calling the cloud setup complete.

References: [AWS CLI temporary login](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sign-in.html), [SDK for .NET authentication](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-idc.html), [S3 POST upload fields](https://docs.aws.amazon.com/AmazonS3/latest/API/RESTObjectPOST.html), [presigned POST conditions](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TS3PostCondition.html), [presigned URL behavior](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html).

Local verification results: 69 integration/storage tests passed across the full regression run and subsequent targeted runs (including 7 attachment workflow cases and 2 real-SDK signing/configuration cases); 15 unit tests and 37 frontend tests passed. Backend build completed with zero warnings/errors; frontend lint and production build passed. Migration `20261106013429_TaskAttachments` was applied successfully to the existing local database. These results do not claim a live AWS upload.

API routes: authenticated `GET /api/attachments/configuration`; under `/api/workspaces/{w}/projects/{p}/tasks/{t}/attachments`, GET lists ready files plus the caller's unexpired pending uploads, POST creates a pending record and signed form, POST `/{id}/finalize` verifies it, and POST `/{id}/download` returns an authorized temporary download URL. Responses carrying grants are marked no-store.

Completion: On November 5, 2026, the user confirmed all 10 manual AWS walkthrough steps passed, including live attachment uploads and downloads. The user approved the Phase 7 commit and starting Phase 8.
