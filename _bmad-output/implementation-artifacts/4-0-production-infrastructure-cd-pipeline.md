# Story 4.0: Production Infrastructure & CD Pipeline

Status: ready-for-dev

## Story

As a **developer/ops**,
I want complete production-ready Kubernetes manifests and a working CD pipeline,
So that the application can be deployed to DigitalOcean Kubernetes and all Epic 4 stories have a real cluster to verify their infrastructure ACs against.

## Acceptance Criteria

1. **Given** a merge to `main` that has passed all CI checks,
   **When** the GitHub Actions deploy workflow runs,
   **Then** Docker images for `gateway`, `shard`, and `admin-api` are built, tagged with both `latest` and `$GITHUB_SHA`, pushed to `registry.digitalocean.com/belterlife/`, and all manifests applied to DOKS via `kubectl apply -f infra/k8s/ -R -n belterlife`

2. **Given** a successful deploy workflow run,
   **When** `kubectl rollout status deployment/gateway deployment/shard deployment/admin-api -n belterlife` is executed,
   **Then** all three report `successfully rolled out` with no manual intervention

3. **Given** the shard `Deployment` manifest,
   **When** inspected,
   **Then** it has `strategy.type: RollingUpdate` with `maxUnavailable: 0` and `maxSurge: 1`, a `lifecycle.preStop` exec hook, and `terminationGracePeriodSeconds: 60` — satisfying the preconditions for Stories 4.5 and 4.6

4. **Given** the gateway `Service` manifest,
   **When** applied to DOKS,
   **Then** `type: LoadBalancer` is set and DigitalOcean provisions a public external IP on port 80 — the gateway is reachable from the internet

5. **Given** the admin-api `Service` manifest,
   **When** applied,
   **Then** `type: ClusterIP` is set — the admin API is never reachable outside the K8s cluster (NFR13)

6. **Given** all three `Deployment` manifests,
   **When** applied,
   **Then** every container has `resources.requests` and `resources.limits` defined — no container can starve or unboundedly consume cluster resources

7. **Given** the `infra/k8s/shard/pdb.yaml` `PodDisruptionBudget`,
   **When** a voluntary disruption (rolling update, node drain) occurs,
   **Then** Kubernetes guarantees at least one shard pod remains available throughout — enforcing the no-mass-disconnection requirement of Story 4.6

8. **Given** `POST /internal/drain` on the shard service,
   **When** called,
   **Then** it returns `HTTP 200 OK` — the stub endpoint exists for the `preStop` hook to invoke; real drain logic is deferred to Story 4.5

9. **Given** the `README.md`,
   **When** consulted by a new operator,
   **Then** a `## Production Setup` section documents the exact `kubectl create secret` command for `belterlife-secrets` and lists all three required GitHub Secrets (`DIGITALOCEAN_ACCESS_TOKEN`, `CLUSTER_NAME`, `REGISTRY_NAME`)

## Tasks / Subtasks

- [ ] Task 1 — Add Namespace manifest (AC: 1, 2)
  - [ ] Create `infra/k8s/namespace.yaml`:
    ```yaml
    apiVersion: v1
    kind: Namespace
    metadata:
      name: belterlife
    ```
  - [ ] Add `namespace: belterlife` to `metadata` in every existing manifest: `gateway/deployment.yaml`, `gateway/service.yaml`, `shard/deployment.yaml`, `shard/service.yaml`, `admin-api/deployment.yaml`, `admin-api/service.yaml`, `configmap.yaml`

- [ ] Task 2 — Complete shard `Deployment` manifest (AC: 3, 6)
  - [ ] Edit `infra/k8s/shard/deployment.yaml` — add under `spec:` (sibling of `selector:`):
    ```yaml
    strategy:
      type: RollingUpdate
      rollingUpdate:
        maxUnavailable: 0
        maxSurge: 1
    ```
  - [ ] Add `terminationGracePeriodSeconds: 60` under `spec.template.spec:`
  - [ ] Add `lifecycle` block under the shard container spec:
    ```yaml
    lifecycle:
      preStop:
        exec:
          command: ["/bin/sh", "-c", "curl -sf -X POST http://localhost:5001/internal/drain || true && sleep 10"]
    ```
  - [ ] Add `resources` block under the shard container spec:
    ```yaml
    resources:
      requests:
        cpu: 250m
        memory: 512Mi
      limits:
        cpu: 1000m
        memory: 1Gi
    ```

- [ ] Task 3 — Complete gateway `Deployment` manifest (AC: 6)
  - [ ] Edit `infra/k8s/gateway/deployment.yaml` — add under `spec:` (sibling of `selector:`):
    ```yaml
    strategy:
      type: RollingUpdate
      rollingUpdate:
        maxUnavailable: 0
        maxSurge: 1
    ```
  - [ ] Add `resources` block under the gateway container spec:
    ```yaml
    resources:
      requests:
        cpu: 100m
        memory: 256Mi
      limits:
        cpu: 500m
        memory: 512Mi
    ```

- [ ] Task 4 — Complete admin-api `Deployment` manifest (AC: 5, 6)
  - [ ] Edit `infra/k8s/admin-api/deployment.yaml` — add `resources` block under the admin-api container spec:
    ```yaml
    resources:
      requests:
        cpu: 50m
        memory: 128Mi
      limits:
        cpu: 200m
        memory: 256Mi
    ```
  - [ ] Confirm `infra/k8s/admin-api/service.yaml` has `type: ClusterIP` (should already be the case — verify only)

- [ ] Task 5 — Make gateway `Service` public (AC: 4)
  - [ ] Edit `infra/k8s/gateway/service.yaml`:
    - Change `type: ClusterIP` to `type: LoadBalancer`
    - Keep `port: 80`, `targetPort: 80`

- [ ] Task 6 — Add `PodDisruptionBudget` for shard (AC: 7)
  - [ ] Create `infra/k8s/shard/pdb.yaml`:
    ```yaml
    apiVersion: policy/v1
    kind: PodDisruptionBudget
    metadata:
      name: shard-pdb
      namespace: belterlife
    spec:
      minAvailable: 1
      selector:
        matchLabels:
          app: shard
    ```

- [ ] Task 7 — Add stub `/internal/drain` endpoint to `BelterLife.Simulation` (AC: 8)
  - [ ] Create `server/BelterLife.Simulation/Api/DrainController.cs`:
    ```csharp
    using Microsoft.AspNetCore.Mvc;

    namespace BelterLife.Simulation.Api;

    [ApiController]
    [Route("internal")]
    public class DrainController : ControllerBase
    {
        // TODO Story 4.5: replace stub with graceful PlayerRedirect drain
        // (send PlayerRedirect to all connected clients, wait for ack, then return 200)
        [HttpPost("drain")]
        public IActionResult Drain() => Ok();
    }
    ```
  - [ ] Confirm `BelterLife.Simulation`'s `Program.cs` has `app.MapControllers()` — add it if absent
  - [ ] Run `cd server && dotnet build BelterLife.slnx` → 0 errors

- [ ] Task 8 — Complete the GitHub Actions deploy workflow (AC: 1, 2)
  - [ ] Replace the entire contents of `.github/workflows/deploy.yml` with:
    ```yaml
    name: Deploy

    on:
      push:
        branches: [main]

    jobs:
      ci-gate:
        name: Wait for CI
        runs-on: ubuntu-latest
        steps:
          - name: Check CI status
            run: echo "Deploy only runs after CI passes (branch protection enforces this)"

      deploy:
        name: Build, Push & Deploy
        runs-on: ubuntu-latest
        needs: ci-gate
        steps:
          - uses: actions/checkout@v4

          - name: Install doctl
            uses: digitalocean/action-doctl@v2
            with:
              token: ${{ secrets.DIGITALOCEAN_ACCESS_TOKEN }}

          - name: Configure kubectl
            run: doctl kubernetes cluster kubeconfig save ${{ secrets.CLUSTER_NAME }}

          - name: Log in to DigitalOcean Container Registry
            run: doctl registry login --expiry-seconds 600

          - name: Setup .NET
            uses: actions/setup-dotnet@v4
            with:
              dotnet-version: '10.x'

          - name: Build & push gateway image
            run: |
              docker build -f infra/docker/Dockerfile.gateway \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/gateway:latest \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/gateway:${{ github.sha }} \
                .
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/gateway:latest
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/gateway:${{ github.sha }}

          - name: Build & push shard image
            run: |
              docker build -f infra/docker/Dockerfile.shard \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/shard:latest \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/shard:${{ github.sha }} \
                .
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/shard:latest
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/shard:${{ github.sha }}

          - name: Build & push admin-api image
            run: |
              docker build -f infra/docker/Dockerfile.admin \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/admin-api:latest \
                -t registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/admin-api:${{ github.sha }} \
                .
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/admin-api:latest
              docker push registry.digitalocean.com/${{ secrets.REGISTRY_NAME }}/admin-api:${{ github.sha }}

          - name: Apply K8s manifests
            run: kubectl apply -f infra/k8s/ -R

          - name: Wait for rollout
            run: |
              kubectl rollout status deployment/gateway -n belterlife --timeout=120s
              kubectl rollout status deployment/shard -n belterlife --timeout=120s
              kubectl rollout status deployment/admin-api -n belterlife --timeout=120s
    ```
  - [ ] Required GitHub Secrets (configure in repo Settings → Secrets → Actions before first deploy):
    - `DIGITALOCEAN_ACCESS_TOKEN` — a DigitalOcean Personal Access Token with read/write scope
    - `CLUSTER_NAME` — the DOKS cluster name (e.g. `belterlife-prod`)
    - `REGISTRY_NAME` — the Container Registry name (e.g. `belterlife`)

- [ ] Task 9 — Document production setup in `README.md` (AC: 9)
  - [ ] Add the following section to `README.md` (after the existing local dev section):
    ```markdown
    ## Production Setup

    ### Required Kubernetes Secret

    Before deploying, create the `belterlife-secrets` Secret in the cluster:

    ```sh
    kubectl create secret generic belterlife-secrets \
      --namespace belterlife \
      --from-literal=ConnectionStrings__Default="Host=...;Database=belterlife;Username=...;Password=..." \
      --from-literal=JwtKey="<min-32-char-random-string>" \
      --from-literal=SHARD_SECRET="<random-shared-secret>"
    ```

    ### Required GitHub Secrets

    Configure these in **Settings → Secrets and variables → Actions** before the first merge to `main`:

    | Secret | Description |
    |---|---|
    | `DIGITALOCEAN_ACCESS_TOKEN` | DigitalOcean Personal Access Token (read/write) |
    | `CLUSTER_NAME` | DOKS cluster name (e.g. `belterlife-prod`) |
    | `REGISTRY_NAME` | Container Registry name (e.g. `belterlife`) |
    ```

- [ ] Task 10 — Dry-run validation (AC: 1)
  - [ ] Run `kubectl apply --dry-run=client -f infra/k8s/ -R` and confirm no validation errors
  - [ ] Run `cd server && dotnet build BelterLife.slnx` → 0 errors
  - [ ] Run `cd server && dotnet test BelterLife.slnx` → 0 failures

## Dev Notes

### Why `maxUnavailable: 0` + `maxSurge: 1` on shard

With game server pods, you must never terminate an old pod before the replacement is fully `Ready`. `maxUnavailable: 0` enforces this — K8s will not remove the old pod until the new one passes its `readinessProbe`. `maxSurge: 1` allows one extra pod to be created during the rollout. This is critical for preventing player disconnection during deployments.

### Why `preStop` + `sleep 10`

When K8s begins terminating a pod, it simultaneously:
1. Executes the `preStop` hook
2. Removes the pod from the Service `Endpoints` list

The sleep gives the gateway (and any load balancer) time to stop routing new connections to the pod before SIGTERM arrives. Existing WebSocket connections are still alive during this window — the stub `/internal/drain` in Story 4.0 returns immediately, but Story 4.5 will replace it with actual `PlayerRedirect` logic. The `terminationGracePeriodSeconds: 60` gives the full drain sequence 60 seconds before a forced kill.

### Image Tagging Strategy

Both `latest` and `$GITHUB_SHA` tags are pushed:
- `latest` — what the K8s manifests reference; `kubectl apply` picks it up on the next pod creation
- `$GITHUB_SHA` — for auditability and rollback (`kubectl set image deployment/shard shard=registry.../shard:<sha>`)

### The Admin Service Must Stay ClusterIP

NFR13 is an architectural security requirement — the admin API must never be reachable outside the cluster. The `ClusterIP` service type enforces this at the network layer. Do not add an Ingress rule for `admin-api`.

### `kubectl apply -f infra/k8s/ -R` applies the Namespace first

Because `namespace.yaml` lives at the root of `infra/k8s/` and Kubernetes processes Namespace objects before Deployment/Service objects, the apply order is safe. If the Namespace already exists, `kubectl apply` is idempotent.

### Docker build context

The Dockerfiles in `infra/docker/` use multi-stage builds referencing `server/`. The `docker build` commands in the workflow run from the repo root (`.`) so the build context includes both `server/` and `client/` at the expected relative paths.
