# RkllmChat

基于 RKLLM Runtime 和 .NET 10 Minimal API 的本地推理服务，封装了 OpenAI 兼容接口（`/v1/*`）和 Ollama 兼容接口（`/api/*`），并包含一个简单的 Angular 前端。

该项目只能运行在 Rockchip RK 平台的 Linux 环境中，例如 `rk3576`、`rk3588`。它依赖 RKLLM Runtime、对应的 NPU 环境和 `.rkllm` 模型文件，不支持普通 `x86_64` 主机，也不支持非 RK 的 ARM 设备。

## 功能

- 聊天推理与流式输出
- OpenAI 风格接口与 Ollama 风格接口
- 基于 `config.json` 的模型与端口配置
- 前端静态文件打包到 `wwwroot`

## 运行前提

运行该项目需要：

- Rockchip RK 平台的 Linux 设备，例如 `rk3576`、`rk3588`
- 对应版本的 RKLLM Runtime 动态库，例如 `librkllmrt.so`
- 已转换好的 `.rkllm` 模型文件
- 如需本地开发或构建：`.NET 10 SDK`、`Node.js` 和 `npm`

> 即使构建过程可以在其他机器中完成，生成的程序实际也只能部署到 RK 平台运行。

---

## 配置说明

根目录下的 `config.json`：

```json
{
  "Rkllm": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 8080
    },
    "ModelPath": "models/Qwen_w4a16.rkllm",
    "Platform": "rk3576",
    "MaxContextLen": 4096,
    "Llm": {
      "MaxNewTokens": 8192,
      "TopK": 50,
      "TopP": 0.95,
      "Temperature": 0.7,
      "RepeatPenalty": 1.1
    },
    "VlModel": {
      "CoreMask": 3,
      "Width": 448,
      "Height": 448,
      "Path": "models/vision.rknn"
    }
  }
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `Server.Host` | 监听地址，支持 `0.0.0.0`、`localhost` 或具体 IP |
| `Server.Port` | 服务监听端口，默认 `8080` |
| `ModelPath` | `.rkllm` 模型文件路径 |
| `Platform` | 平台类型，如 `rk3576` / `rk3588` |
| `MaxContextLen` | 最大上下文长度 |
| `Llm` | 常用生成参数，例如 `Temperature`、`TopP`、`TopK` |
| `VlModel` | 多模态视觉模型配置；不需要视觉能力时可省略 |

也可以通过命令行指定配置文件路径：

```bash
./rkllm-chat-server -c /path/to/config.json
./rkllm-chat-server --config ./configs/prod.json --debug
```

---

## 快速启动

### 1. 准备模型与运行库

请确保：

- 模型文件已放入 `models/`，例如 `models/Qwen_w4a16.rkllm`
- RKLLM 原生依赖可被程序找到，例如放在系统库路径或随程序一起部署

### 2. 启动后端

以下命令应在 RK 设备上执行：

```bash
dotnet run --project RkllmChat.csproj
```

如需开启调试日志：

```bash
dotnet run --project RkllmChat.csproj -- --debug
```

如需指定配置文件：

```bash
dotnet run --project RkllmChat.csproj -- -c ./config.json
```

服务默认地址：

```text
http://127.0.0.1:8080
```

健康检查：

```bash
curl http://127.0.0.1:8080/health
```

---

## 前端开发

前端位于 `ui/`。开发时会通过代理将 `/api/*` 转发到本地后端 `http://127.0.0.1:8080`。

```bash
cd ui
npm install
npm start
```

启动后访问：

```text
http://localhost:4200
```

---

## API 示例

### Ollama 兼容接口

#### 聊天

```bash
curl http://127.0.0.1:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": false,
    "messages": [
      { "role": "user", "content": "写一个 Bash 脚本，找出当前目录下最大的 10 个文件。" }
    ]
  }'
```

#### 生成

```bash
curl http://127.0.0.1:8080/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": false,
    "prompt": "用 Python 写一个函数，读取 JSON 文件并统计每个顶层键出现的次数。"
  }'
```

### OpenAI 兼容接口

#### Chat Completions

```bash
curl http://127.0.0.1:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": true,
    "messages": [
      { "role": "user", "content": "写一个 Bash 脚本，检查 8080 端口是否被占用，并输出对应进程信息。" }
    ]
  }'
```

---

## 构建与导出

可以使用项目内的脚本完成前后端打包：

```bash
chmod +x scripts/build.sh
./scripts/build.sh
```

默认流程：

1. 构建 Angular UI
2. 执行 `dotnet publish`
3. 将前端产物复制到 `publish/wwwroot`
4. 在 `dist/` 下生成压缩包和 `.deb` 安装包

默认输出示例：

```text
dist/rkllm-linux-arm64.tar.gz
dist/rkllm-chat_0.1.0_arm64.deb
```

发布目录中的服务可执行文件名为：

```text
rkllm-chat-server
```

也可以单独构建 Debian 包：

```bash
CONFIGURATION=Release RUNTIME=linux-arm64 ./scripts/build.sh deb
# 或者
PACKAGE_VERSION=0.1.0 ./scripts/package-deb.sh
```

也可以通过环境变量覆盖目标运行时：

```bash
CONFIGURATION=Release RUNTIME=linux-arm64 ./scripts/build.sh
```

### 安装 `.deb` 并启用 `systemd`

```bash
sudo dpkg -i dist/rkllm-chat_0.1.0_arm64.deb
sudo systemctl daemon-reload
sudo systemctl enable --now rkllm-chat.service
sudo systemctl status rkllm-chat.service
```

服务默认：

- 安装目录：`/opt/rkllm-chat`
- 服务名：`rkllm-chat.service`
- 可选环境文件：`/etc/default/rkllm-chat`

> `.deb` 默认**不打包任何 `.so` / `libs` 动态库**，也**不提供任何模型文件**。
> 目标 RK 设备需要自行准备运行库和 `.rkllm` / `.rknn` 模型，并按你的部署路径修改 `config.json`。

安装完成后，请在目标设备**自行准备模型和运行库**，例如：

```bash
sudo mkdir -p /opt/rkllm-chat/models
# 自行复制你的模型
# /opt/rkllm-chat/models/Qwen_w4a16.rkllm
# 并确保系统库路径中可找到 librkllmrt.so 等运行库
```

如需追加参数（例如 `--debug` 或指定配置文件），编辑：

```bash
sudo nano /etc/default/rkllm-chat
```

例如：

```bash
RKLLM_CHAT_ARGS="--debug --config /opt/rkllm-chat/config.json"
```

### 使用 Docker 导出编译产物

根目录提供了一个基于 `ubuntu:22.04` 的 `Dockerfile`。它会在容器内安装 `.NET 10 SDK`、`Node.js` 和构建依赖，然后直接执行 `scripts/build.sh`。

```bash
docker buildx build \
  --platform linux/arm64 \
  --build-arg RUNTIME=linux-arm64 \
  --output type=local,dest=./docker-out \
  .
```

导出后可在 `docker-out/` 中看到：

- `rkllm-linux-arm64/`：发布目录，其中主程序为 `rkllm-chat-server`
- `rkllm-linux-arm64.tar.gz`：压缩包

> 如果是在非 ARM 主机上构建 `linux-arm64`，需要可用的 `buildx` / QEMU 环境。
>
> 生成的程序仍然只能部署到 RK 平台运行。当前 `.dockerignore` 默认排除了 `models/` 和 `libs/`，如需随包部署请自行复制。

---

## 已实现接口

### 基础接口

- `GET /health`

### Ollama 风格

- `GET /api/version`
- `GET /api/tags`
- `GET /api/ps`
- `POST /api/show`
- `POST /api/generate`
- `POST /api/chat`

### OpenAI 风格

- `POST /v1/chat/completions`

---

## 当前限制

- Embedding 相关接口暂未提供
- 服务当前一次只处理一个请求，并发请求在模型忙时会返回 `503 Server busy`

---

## 注意事项

- `config.json` 中的相对模型路径会自动解析为配置文件所在目录下的实际路径
- `.deb` 安装包默认不包含模型文件与 `.so` 运行库，请在目标设备单独准备
- 如果前端未构建，后端仍可单独作为 API 服务运行
- 运行环境必须是 RK 平台；仅完成编译并不表示可以在其他平台直接运行
- `MOTD.txt` 会作为嵌入资源打包到可执行文件中，并在启动时输出
