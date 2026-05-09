#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

LOCAL_BUILD_DIR="${LOCAL_BUILD_DIR:-${PROJECT_ROOT}/beat-striker/Dist/RelayServer}"
LOCAL_COMPOSE_FILE="${LOCAL_COMPOSE_FILE:-${SCRIPT_DIR}/docker-compose.yml}"
LOCAL_STAGE_DIR="${LOCAL_STAGE_DIR:-${SCRIPT_DIR}/.stage}"

# SSH は ~/.ssh/config の Host エイリアスのみ（例: Host rin → User / HostName / Port / IdentityFile はすべて config）。
# REMOTE_SSH_TARGET を優先。互換で REMOTE_HOST に同じ Host 名だけを渡してもよい。
REMOTE_SSH_TARGET="${REMOTE_SSH_TARGET:-}"
REMOTE_HOST="${REMOTE_HOST:-}"
# Default under remote $HOME (no /opt; normal users can mkdir without sudo).
REMOTE_DIR="${REMOTE_DIR:-beat-striker/relay}"

RELAY_BUILD_SUBDIR="${RELAY_BUILD_SUBDIR:-RelayServerLinux}"

DEFAULT_REMOTE_DIR="${REMOTE_DIR}"
DEFAULT_LOCAL_BUILD_DIR="${LOCAL_BUILD_DIR}"
DEFAULT_LOCAL_COMPOSE_FILE="${LOCAL_COMPOSE_FILE}"
DEFAULT_RELAY_BUILD_SUBDIR="${RELAY_BUILD_SUBDIR}"

remote_deploy_display() {
  local d="$1"
  if [[ "${d}" == /* ]]; then
    printf '%s\n' "${d}"
  else
    printf '~/%s\n' "${d}"
  fi
}

remote_mkdir_expr() {
  local d="$1"
  if [[ "${d}" == /* ]]; then
    printf 'mkdir -p %q' "${d}"
  else
    printf 'mkdir -p "$HOME/%s"' "${d}"
  fi
}

remote_cd_expr() {
  local d="$1"
  if [[ "${d}" == /* ]]; then
    printf 'cd %q' "${d}"
  else
    printf 'cd "$HOME/%s"' "${d}"
  fi
}

rsync_remote_dest() {
  local dest="$1"
  local path="$2"
  if [[ "${path}" == /* ]]; then
    printf '%s:%s/' "${dest}" "${path}"
  else
    printf '%s:~/%s/' "${dest}" "${path}"
  fi
}

# Remote shell で docker compose が見つける設定ファイルのあるディレクトリ（表示用）
remote_compose_parent_dir() {
  local d="$1"
  if [[ "${d}" == /* ]]; then
    printf '%s\n' "${d}"
  else
    printf '~/%s\n' "${d}"
  fi
}

prompt_required() {
  local prompt_label="$1"
  local current_value="$2"
  local input_value=""
  while true; do
    if [[ -n "${current_value}" ]]; then
      read -r -p "${prompt_label} [${current_value}]: " input_value
      if [[ -z "${input_value}" ]]; then
        printf '%s\n' "${current_value}"
        return
      fi
      printf '%s\n' "${input_value}"
      return
    fi

    read -r -p "${prompt_label}: " input_value
    if [[ -n "${input_value}" ]]; then
      printf '%s\n' "${input_value}"
      return
    fi
    echo "This field is required."
  done
}

prompt_optional() {
  local prompt_label="$1"
  local default_value="$2"
  local input_value=""
  read -r -p "${prompt_label} [${default_value}]: " input_value
  if [[ -z "${input_value}" ]]; then
    printf '%s\n' "${default_value}"
    return
  fi
  printf '%s\n' "${input_value}"
}

ssh_host_from_env() {
  if [[ -n "${REMOTE_SSH_TARGET}" ]]; then
    printf '%s\n' "${REMOTE_SSH_TARGET}"
    return
  fi
  if [[ -n "${REMOTE_HOST}" ]]; then
    printf '%s\n' "${REMOTE_HOST}"
    return
  fi
  printf ''
}

echo "=== Relay deploy settings ==="
SSH_HOST="$(ssh_host_from_env)"
if [[ -n "${SSH_HOST}" ]]; then
  echo "SSH Host: ${SSH_HOST} (from ~/.ssh/config — User / Port / IdentityFile は聞かない)"
else
  SSH_HOST="$(prompt_required "SSH Host (~/.ssh/config の Host 名)" "")"
  echo "SSH Host: ${SSH_HOST}"
fi

REMOTE_DIR="$(prompt_optional "Remote deploy path (\$HOME/<path> or absolute)" "${DEFAULT_REMOTE_DIR}")"
LOCAL_BUILD_DIR="$(prompt_optional "Local build directory" "${DEFAULT_LOCAL_BUILD_DIR}")"
LOCAL_COMPOSE_FILE="$(prompt_optional "Local compose file path" "${DEFAULT_LOCAL_COMPOSE_FILE}")"
RELAY_BUILD_SUBDIR="$(prompt_optional "Build directory name on remote" "${DEFAULT_RELAY_BUILD_SUBDIR}")"

echo
echo "Deploy target:"
echo "  ${SSH_HOST}:$(remote_deploy_display "${REMOTE_DIR}")"
echo "Source:"
echo "  ${LOCAL_BUILD_DIR}"
echo "Compose:"
echo "  ${LOCAL_COMPOSE_FILE}"
echo
read -r -p "Continue deployment? [y/N]: " CONFIRM
if [[ ! "${CONFIRM}" =~ ^[Yy]$ ]]; then
  echo "Cancelled."
  exit 0
fi

if [[ ! -d "${LOCAL_BUILD_DIR}" ]]; then
  echo "Local build directory not found: ${LOCAL_BUILD_DIR}"
  exit 1
fi

if [[ ! -f "${LOCAL_COMPOSE_FILE}" ]]; then
  echo "Compose file not found: ${LOCAL_COMPOSE_FILE}"
  exit 1
fi

rm -rf "${LOCAL_STAGE_DIR}"
mkdir -p "${LOCAL_STAGE_DIR}/${RELAY_BUILD_SUBDIR}"

rsync -az --delete \
  --exclude '.DS_Store' \
  --exclude '*_BurstDebugInformation_DoNotShip' \
  "${LOCAL_BUILD_DIR}/" \
  "${LOCAL_STAGE_DIR}/${RELAY_BUILD_SUBDIR}/"

cp "${LOCAL_COMPOSE_FILE}" "${LOCAL_STAGE_DIR}/docker-compose.yml"
COMPOSE_DIR="$(cd "$(dirname "${LOCAL_COMPOSE_FILE}")" && pwd)"
if [[ ! -f "${COMPOSE_DIR}/Dockerfile" ]]; then
  echo "Dockerfile not found (expect ${COMPOSE_DIR}/Dockerfile next to compose)."
  exit 1
fi
cp "${COMPOSE_DIR}/Dockerfile" "${LOCAL_STAGE_DIR}/Dockerfile"

SSH_OPTS=(-o StrictHostKeyChecking=accept-new)
RSYNC_SSH="ssh ${SSH_OPTS[*]}"

REMOTE_MKDIR="$(remote_mkdir_expr "${REMOTE_DIR}")"
ssh "${SSH_OPTS[@]}" "${SSH_HOST}" "${REMOTE_MKDIR}"

RSYNC_DEST="$(rsync_remote_dest "${SSH_HOST}" "${REMOTE_DIR}")"
rsync -az --delete \
  -e "${RSYNC_SSH}" \
  "${LOCAL_STAGE_DIR}/" \
  "${RSYNC_DEST}"

REMOTE_CD="$(remote_cd_expr "${REMOTE_DIR}")"
ssh "${SSH_OPTS[@]}" "${SSH_HOST}" \
  "${REMOTE_CD} && docker compose up -d --build --force-recreate"

REMOTE_COMPOSE_DIR="$(remote_compose_parent_dir "${REMOTE_DIR}")"
echo "Deploy completed: ${SSH_HOST}:$(remote_deploy_display "${REMOTE_DIR}")"
echo ""
echo "docker compose は docker-compose.yml があるディレクトリで実行する必要があります（~/ だけだと「no configuration file provided」になります）。"
echo "  ssh ${SSH_HOST}"
echo "  cd ${REMOTE_COMPOSE_DIR}"
echo "  docker compose logs --tail=200 relay"
