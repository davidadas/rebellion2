ARG BASE_IMAGE=unityci/editor:ubuntu-6000.4.12f1-linux-il2cpp-3
FROM ${BASE_IMAGE}
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libvulkan1 mesa-vulkan-drivers \
    && rm -rf /var/lib/apt/lists/*
