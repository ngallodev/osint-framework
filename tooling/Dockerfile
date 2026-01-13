FROM kalilinux/kali-rolling

ENV DEBIAN_FRONTEND=noninteractive \
    TOOLING_HOME=/opt/osint-tools

RUN apt-get update && \
    apt install -y kali-linux-headless \
        python3 \
        python3-pip \
        python3-venv \
        git \
        theharvester \
        ripgrep \
        curl \
        jq \
        build-essential && \
    rm -rf /var/lib/apt/lists/*

# # Install Sherlock into a dedicated virtual environment
# RUN mkdir -p ${TOOLING_HOME} && \
#     git clone https://github.com/sherlock-project/sherlock.git ${TOOLING_HOME}/sherlock && \
#     python3 -m venv ${TOOLING_HOME}/sherlock/.venv && \
#     ${TOOLING_HOME}/sherlock/.venv/bin/pip install --no-cache-dir -U pip && \
#     cd ${TOOLING_HOME}/sherlock && \
#     if [ -f requirements.txt ]; then \
#         ${TOOLING_HOME}/sherlock/.venv/bin/pip install --no-cache-dir -r requirements.txt; \
#     else \
#         ${TOOLING_HOME}/sherlock/.venv/bin/pip install --no-cache-dir .; \
#     fi

# Provide convenience wrapper script on PATH
# RUN printf '#!/bin/bash\nsource %s/sherlock/.venv/bin/activate\nexec python %s/sherlock/sherlock.py "$@"\n' "$TOOLING_HOME" "$TOOLING_HOME" > /usr/local/bin/sherlock && \
#     chmod +x /usr/local/bin/sherlock

# # Install Tookie OSINT into its own virtual environment
# RUN git clone https://github.com/AstraaDev/tookie.git ${TOOLING_HOME}/tookie && \
#     python3 -m venv ${TOOLING_HOME}/tookie/.venv && \
#     ${TOOLING_HOME}/tookie/.venv/bin/pip install --no-cache-dir -U pip && \
#     cd ${TOOLING_HOME}/tookie && \
#     if [ -f requirements.txt ]; then \
#         ${TOOLING_HOME}/tookie/.venv/bin/pip install --no-cache-dir -r requirements.txt; \
#     else \
#         ${TOOLING_HOME}/tookie/.venv/bin/pip install --no-cache-dir .; \
#     fi && \
#     ln -s ${TOOLING_HOME}/tookie/tookie.py /usr/local/bin/tookie-osint && \
#     printf '#!/bin/bash\nsource %s/tookie/.venv/bin/activate\nexec python %s/tookie/tookie.py "$@"\n' "$TOOLING_HOME" "$TOOLING_HOME" > /usr/local/bin/tookie && \
#     chmod +x /usr/local/bin/tookie /usr/local/bin/tookie-osint

WORKDIR /workspace

CMD ["sleep", "infinity"]
