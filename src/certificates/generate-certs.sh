#!/bin/bash
set -e

CERT_DIR="$(cd "$(dirname "$0")" && pwd)"
CA_PASSWORD="dev"
CERT_PASSWORD="dev"
DAYS_VALID=365

if [ ! -f "$CERT_DIR/ca.key" ] || [ ! -f "$CERT_DIR/ca.crt" ]; then
  echo "=== Generating Internal CA ==="
  openssl genrsa -out "$CERT_DIR/ca.key" 4096

  openssl req -x509 -new -nodes \
    -key "$CERT_DIR/ca.key" \
    -sha256 \
    -days $DAYS_VALID \
    -out "$CERT_DIR/ca.crt" \
    -subj "/CN=SocketChat Internal CA/O=SocketChat"

  echo "=== CA certificate created ==="
else
  echo "=== Reusing existing CA at $CERT_DIR/ca.crt ==="
fi

# Function to generate a service certificate signed by the CA
generate_service_cert() {
  local SERVICE_NAME=$1
  local DNS_NAMES=$2

  echo "--- Generating certificate for: $SERVICE_NAME ---"

  # Create a temporary config file for SAN (Subject Alternative Names)
  cat > "$CERT_DIR/${SERVICE_NAME}.cnf" <<EOF
[req]
default_bits = 2048
prompt = no
distinguished_name = dn
req_extensions = v3_req

[dn]
CN = ${SERVICE_NAME}

[v3_req]
subjectAltName = ${DNS_NAMES}
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
EOF

  # Generate private key
  openssl genrsa -out "$CERT_DIR/${SERVICE_NAME}.key" 2048

  # Generate CSR
  openssl req -new \
    -key "$CERT_DIR/${SERVICE_NAME}.key" \
    -out "$CERT_DIR/${SERVICE_NAME}.csr" \
    -config "$CERT_DIR/${SERVICE_NAME}.cnf"

  # Sign with CA
  openssl x509 -req \
    -in "$CERT_DIR/${SERVICE_NAME}.csr" \
    -CA "$CERT_DIR/ca.crt" \
    -CAkey "$CERT_DIR/ca.key" \
    -CAcreateserial \
    -out "$CERT_DIR/${SERVICE_NAME}.crt" \
    -days $DAYS_VALID \
    -sha256 \
    -extensions v3_req \
    -extfile "$CERT_DIR/${SERVICE_NAME}.cnf"

  # Export to PFX (needed by ASP.NET Core / Kestrel)
  openssl pkcs12 -export \
    -out "$CERT_DIR/${SERVICE_NAME}.pfx" \
    -inkey "$CERT_DIR/${SERVICE_NAME}.key" \
    -in "$CERT_DIR/${SERVICE_NAME}.crt" \
    -certfile "$CERT_DIR/ca.crt" \
    -passout pass:$CERT_PASSWORD

  # Clean up intermediate files
  rm -f "$CERT_DIR/${SERVICE_NAME}.csr" "$CERT_DIR/${SERVICE_NAME}.cnf"

  echo "    -> ${SERVICE_NAME}.pfx created"
}

# Generate certificates for each service
# DNS names include Docker service names and Kubernetes service DNS names
NS="socketchat"
generate_service_cert "identity"             "DNS:identity.api,DNS:identity-api,DNS:identity-api.${NS},DNS:identity-api.${NS}.svc.cluster.local,DNS:localhost"
generate_service_cert "chat-api"             "DNS:chat.api,DNS:chat-api,DNS:chat-api.${NS},DNS:chat-api.${NS}.svc.cluster.local,DNS:localhost"
generate_service_cert "chat-eventprocessor"  "DNS:chat.eventprocessor,DNS:chat-eventprocessor,DNS:chat-eventprocessor.${NS},DNS:chat-eventprocessor.${NS}.svc.cluster.local,DNS:localhost"

echo ""
echo "=== Certificates generated ==="
echo "CA certificate: $CERT_DIR/ca.crt"
echo "CA private key: $CERT_DIR/ca.key (keep this secure!)"
echo ""
echo "All .pfx files use password: $CERT_PASSWORD"
