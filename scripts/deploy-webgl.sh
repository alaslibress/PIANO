#!/usr/bin/env bash
# deploy-webgl.sh — Sube el build WebGL de Unity a S3 y limpia cache en CloudFront.
#
# Variables de entorno requeridas:
#   REHAB_S3_BUCKET   — nombre del bucket S3 (ej. rehabiapp-games-piano)
#   REHAB_CF_DIST_ID  — Distribution ID de CloudFront (ej. E1ABCDEF123456)
#
# Opcional:
#   AWS_PROFILE       — perfil AWS CLI (default: rehabiapp)
#
# Uso:
#   export REHAB_S3_BUCKET=rehabiapp-games-piano
#   export REHAB_CF_DIST_ID=E1ABCDEF123456
#   ./scripts/deploy-webgl.sh

set -euo pipefail

BUCKET="${REHAB_S3_BUCKET:?Falta REHAB_S3_BUCKET}"
DIST_ID="${REHAB_CF_DIST_ID:?Falta REHAB_CF_DIST_ID}"
PROFILE="${AWS_PROFILE:-rehabiapp}"
# Ruta al build WebGL. Puede ser absoluta o relativa al directorio del proyecto.
# Default: donde Unity GUI guarda el build cuando se elige /DAM/proyecto/PIANO/
BUILD_DIR="${REHAB_BUILD_DIR:-/home/alaslibres/DAM/proyecto/PIANO}"

[ -d "$BUILD_DIR" ] || {
    echo "[deploy] ERROR: $BUILD_DIR no existe."
    echo "         Ajusta REHAB_BUILD_DIR o copia el build a esa ruta."
    exit 1
}

echo "[deploy] Subiendo assets a s3://$BUCKET/ ..."

# Archivos con Brotli precomprimido: subir con Content-Encoding y Content-Type correctos.
# CloudFront Function se encarga de propagar los headers al navegador.
aws s3 sync "$BUILD_DIR/Build/" "s3://$BUCKET/Build/" \
    --profile "$PROFILE" \
    --delete \
    --cache-control "public,max-age=31536000,immutable" \
    --exclude "*.br" \
    --exclude "*.gz"

# Archivos .wasm.br
aws s3 sync "$BUILD_DIR/Build/" "s3://$BUCKET/Build/" \
    --profile "$PROFILE" \
    --cache-control "public,max-age=31536000,immutable" \
    --exclude "*" \
    --include "*.wasm.br" \
    --content-encoding "br" \
    --content-type "application/wasm"

# Archivos .js.br
aws s3 sync "$BUILD_DIR/Build/" "s3://$BUCKET/Build/" \
    --profile "$PROFILE" \
    --cache-control "public,max-age=31536000,immutable" \
    --exclude "*" \
    --include "*.js.br" \
    --content-encoding "br" \
    --content-type "application/javascript"

# Archivos .data.br
aws s3 sync "$BUILD_DIR/Build/" "s3://$BUCKET/Build/" \
    --profile "$PROFILE" \
    --cache-control "public,max-age=31536000,immutable" \
    --exclude "*" \
    --include "*.data.br" \
    --content-encoding "br" \
    --content-type "application/octet-stream"

# TemplateData y demas archivos estaticos
aws s3 sync "$BUILD_DIR/TemplateData/" "s3://$BUCKET/TemplateData/" \
    --profile "$PROFILE" \
    --delete \
    --cache-control "public,max-age=31536000,immutable"

# index.html con cache corto para que nuevos deploys lleguen inmediatamente
aws s3 cp "$BUILD_DIR/index.html" "s3://$BUCKET/index.html" \
    --profile "$PROFILE" \
    --cache-control "no-cache,max-age=0"

echo "[deploy] Assets subidos. Invalidando CloudFront $DIST_ID ..."

INVALIDATION_ID=$(aws cloudfront create-invalidation \
    --distribution-id "$DIST_ID" \
    --paths "/*" \
    --profile "$PROFILE" \
    --query 'Invalidation.Id' \
    --output text)

echo "[deploy] Invalidacion creada: $INVALIDATION_ID"

DOMAIN=$(aws cloudfront get-distribution \
    --id "$DIST_ID" \
    --profile "$PROFILE" \
    --query 'Distribution.DomainName' \
    --output text)

echo "[deploy] OK. URL publica: https://$DOMAIN"
echo "[deploy] Para probar con token:"
echo "         https://$DOMAIN?dni=DNI_PACIENTE&token=JWT_TOKEN&api=https://API_URL"
