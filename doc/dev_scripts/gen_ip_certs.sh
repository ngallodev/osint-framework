# script to generate key + cert for dev-ip.conf
openssl req -x509 -nodes -days 365 \
  -newkey rsa:2048 \
  -keyout certificates/dev-ip.key \
  -out certificates/dev-ip.crt \
  -config certificates/dev-ip.conf




# generate pfx for kestrel

openssl pkcs12 -export \
  -in certificates/dev-ip.crt \
  -inkey certificates/dev-ip.key \
  -out certificates/dev-ip.pfx \
  -password pass:localdevpass
