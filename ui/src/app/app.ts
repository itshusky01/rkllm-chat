import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

type ChatRole = 'user' | 'assistant';

interface ImageAttachment {
  name: string;
  dataUrl: string;
}

type MessagePayloadPart =
  | { type: 'text'; text: string }
  | { type: 'image_url'; image_url: { url: string } };

type MessagePayloadContent = string | MessagePayloadPart[];

interface ChatMessage {
  id: number;
  role: ChatRole;
  content: string;
  time: string;
  includeInContext: boolean;
  streaming: boolean;
  attachments: ImageAttachment[];
  requestContent: MessagePayloadContent;
}

interface OllamaTagsResponse {
  models?: Array<{
    name?: string;
    model?: string;
  }>;
}

interface StreamChatResponse {
  message?: {
    role?: string;
    content?: string;
  };
  response?: string;
  done?: boolean;
  error?: string;
  choices?: Array<{
    delta?: {
      content?: string;
    };
  }>;
}

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  protected readonly title = 'RKLLM Chat';

  protected readonly apiBase = signal('/api');
  protected readonly model = signal('');
  protected readonly systemPrompt = signal('You are a concise, helpful assistant. Always reply in English.');
  protected readonly composer = signal('');
  protected readonly pendingImages = signal<ImageAttachment[]>([]);
  protected readonly isDragOver = signal(false);
  protected readonly loading = signal(false);
  protected readonly refreshingModels = signal(false);
  protected readonly status = signal<'connecting' | 'ready' | 'error'>('connecting');
  protected readonly errorMessage = signal('');
  protected readonly models = signal<string[]>([]);
  protected readonly messages = signal<ChatMessage[]>([
    this.createMessage('assistant', 'Hi, I\'m ready. Ask anything.', false)
  ]);

  protected readonly canSend = computed(
    () =>
      !this.loading() &&
      this.model().trim().length > 0 &&
      (this.composer().trim().length > 0 || this.pendingImages().length > 0)
  );
  protected readonly statusLabel = computed(() => {
    switch (this.status()) {
      case 'ready':
        return 'Connected';
      case 'error':
        return 'Connection error';
      default:
        return 'Connecting';
    }
  });
  protected readonly chatEndpoint = computed(() => this.buildApiUrl('/chat'));
  protected readonly tagsEndpoint = computed(() => this.buildApiUrl('/tags'));

  constructor() {
    void this.refreshModels();
  }

  protected async refreshModels(): Promise<void> {
    this.refreshingModels.set(true);
    this.errorMessage.set('');

    try {
      const response = await fetch(this.tagsEndpoint());
      if (!response.ok) {
        throw new Error((await response.text()).trim() || `HTTP ${response.status}`);
      }

      const data = (await response.json()) as OllamaTagsResponse;
      const names = (data.models ?? [])
        .map((item) => item.name ?? item.model ?? '')
        .filter((item) => item.trim().length > 0);

      this.models.set(names);
      if (!this.model() && names.length > 0) {
        this.model.set(names[0]);
      }

      this.status.set('ready');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load models';
      this.status.set('error');
      this.errorMessage.set(`Failed to load models: ${message}`);
      this.models.set([]);
    } finally {
      this.refreshingModels.set(false);
    }
  }

  protected async sendMessage(prefill?: string): Promise<void> {
    const content = (prefill ?? this.composer()).trim();
    const attachments = [...this.pendingImages()];
    if ((!content && attachments.length === 0) || this.loading()) {
      return;
    }

    if (!this.model().trim()) {
      this.errorMessage.set('Please select a model before sending a message.');
      return;
    }

    this.errorMessage.set('');

    const requestContent = this.buildRequestContent(content, attachments);
    const userMessage = this.createMessage('user', content, true, false, requestContent, attachments);
    const conversation = [
      ...this.messages()
        .filter((item) => item.includeInContext)
        .map(({ role, requestContent: messageContent }) => ({ role, content: messageContent })),
      { role: 'user', content: requestContent }
    ];

    const assistantMessage = this.createMessage('assistant', '', false, true);

    this.messages.update((items) => [...items, userMessage, assistantMessage]);
    this.composer.set('');
    this.pendingImages.set([]);
    this.loading.set(true);

    try {
      const payload: {
        model?: string;
        stream: boolean;
        messages: Array<{ role: string; content: MessagePayloadContent }>;
      } = {
        stream: true,
        messages: [
          ...(this.systemPrompt().trim()
            ? [{ role: 'system', content: this.systemPrompt().trim() }]
            : []),
          ...conversation
        ]
      };

      if (this.model().trim()) {
        payload.model = this.model().trim();
      }

      const response = await fetch(this.chatEndpoint(), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        throw new Error((await response.text()).trim() || `HTTP ${response.status}`);
      }

      await this.consumeStreamResponse(response, assistantMessage.id);

      const finalMessage = this.messages().find((item) => item.id === assistantMessage.id);
      const finalContent = finalMessage?.content.trim() || 'No content was returned by the model.';
      this.updateMessage(assistantMessage.id, (message) => ({
        ...message,
        content: finalContent,
        requestContent: finalContent,
        includeInContext: true,
        streaming: false
      }));
      this.status.set('ready');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Request failed';
      this.errorMessage.set(`Request failed: ${message}`);
      this.status.set('error');
      this.updateMessage(assistantMessage.id, (item) => ({
        ...item,
        content: `⚠️ Request failed: ${message}`,
        includeInContext: false,
        streaming: false
      }));
    } finally {
      this.loading.set(false);
    }
  }

  protected clearChat(): void {
    this.messages.set([
      this.createMessage('assistant', 'Chat cleared. Start a new conversation anytime.', false)
    ]);
    this.pendingImages.set([]);
    this.errorMessage.set('');
  }

  protected async handleImageSelection(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);

    await this.appendImageFiles(files);
    input.value = '';
  }

  protected handleDragOver(event: DragEvent): void {
    event.preventDefault();

    if (!this.hasImageFiles(event.dataTransfer)) {
      return;
    }

    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }

    this.isDragOver.set(true);
  }

  protected handleDragLeave(event: DragEvent): void {
    const currentTarget = event.currentTarget as HTMLElement | null;
    const relatedTarget = event.relatedTarget as Node | null;

    if (!currentTarget || !relatedTarget || !currentTarget.contains(relatedTarget)) {
      this.isDragOver.set(false);
    }
  }

  protected async handleDrop(event: DragEvent): Promise<void> {
    event.preventDefault();
    this.isDragOver.set(false);

    const files = Array.from(event.dataTransfer?.files ?? []);
    await this.appendImageFiles(files);
  }

  protected removePendingImage(index: number): void {
    this.pendingImages.update((items) => items.filter((_, currentIndex) => currentIndex !== index));
  }

  protected handleKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault();
      void this.sendMessage();
    }
  }

  private async consumeStreamResponse(response: Response, messageId: number): Promise<void> {
    if (!response.body) {
      const data = (await response.json()) as StreamChatResponse;
      const reply = this.extractDeltaText(data) || 'No content was returned by the model.';
      await this.animateChunk(messageId, reply);
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      buffer = await this.flushBuffer(buffer, messageId);
    }

    buffer += decoder.decode();
    if (buffer.trim()) {
      await this.processStreamLine(buffer, messageId);
    }
  }

  private async flushBuffer(buffer: string, messageId: number): Promise<string> {
    const lines = buffer.split(/\r?\n/);
    const remainder = lines.pop() ?? '';

    for (const line of lines) {
      await this.processStreamLine(line, messageId);
    }

    return remainder;
  }

  private async processStreamLine(rawLine: string, messageId: number): Promise<void> {
    const line = rawLine.trim();
    if (!line || line.startsWith(':') || line.startsWith('event:')) {
      return;
    }

    const payloadText = line.startsWith('data:') ? line.slice(5).trim() : line;
    if (!payloadText || payloadText === '[DONE]') {
      return;
    }

    let payload: StreamChatResponse;
    try {
      payload = JSON.parse(payloadText) as StreamChatResponse;
    } catch {
      return;
    }

    if (payload.error) {
      throw new Error(payload.error);
    }

    const delta = this.extractDeltaText(payload);
    if (delta) {
      await this.animateChunk(messageId, delta);
    }
  }

  private extractDeltaText(payload: StreamChatResponse): string {
    const openAiDelta = (payload.choices ?? [])
      .map((choice) => choice.delta?.content ?? '')
      .join('');

    return payload.message?.content ?? payload.response ?? openAiDelta;
  }

  private async animateChunk(messageId: number, chunk: string): Promise<void> {
    const pieces = this.splitIntoPieces(chunk);

    for (const piece of pieces) {
      this.updateMessage(messageId, (message) => ({
        ...message,
        content: `${message.content}${piece}`
      }));

      await this.wait(this.getTypingDelay(piece));
    }
  }

  private splitIntoPieces(chunk: string): string[] {
    if (!chunk) {
      return [];
    }

    if (/[\u4e00-\u9fff]/.test(chunk)) {
      return Array.from(chunk);
    }

    return chunk.match(/\s+|[^\s]+/g) ?? [chunk];
  }

  private getTypingDelay(piece: string): number {
    if (!piece.trim()) {
      return 0;
    }

    return /[\u4e00-\u9fff]/.test(piece) ? 24 : 36;
  }

  private wait(milliseconds: number): Promise<void> {
    return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
  }

  private async appendImageFiles(files: File[]): Promise<void> {
    const imageFiles = files.filter((file) => file.type.startsWith('image/'));
    if (imageFiles.length === 0) {
      return;
    }

    try {
      const attachments = await Promise.all(imageFiles.map((file) => this.toImageAttachment(file)));
      this.pendingImages.update((items) => [...items, ...attachments]);
      this.errorMessage.set('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not read image file';
      this.errorMessage.set(`Failed to load image: ${message}`);
    }
  }

  private hasImageFiles(dataTransfer: DataTransfer | null): boolean {
    if (!dataTransfer) {
      return false;
    }

    return Array.from(dataTransfer.items).some(
      (item) => item.kind === 'file' && item.type.startsWith('image/')
    );
  }

  private buildRequestContent(content: string, attachments: ImageAttachment[]): MessagePayloadContent {
    if (attachments.length === 0) {
      return content;
    }

    const parts: MessagePayloadPart[] = attachments.map((attachment) => ({
      type: 'image_url',
      image_url: {
        url: attachment.dataUrl
      }
    }));

    if (content) {
      parts.push({
        type: 'text',
        text: content
      });
    }

    return parts;
  }

  private async toImageAttachment(file: File): Promise<ImageAttachment> {
    return {
      name: file.name,
      dataUrl: await this.readFileAsDataUrl(file)
    };
  }

  private readFileAsDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result ?? ''));
      reader.onerror = () => reject(reader.error ?? new Error(`Failed to read ${file.name}`));
      reader.readAsDataURL(file);
    });
  }

  private updateMessage(messageId: number, updater: (message: ChatMessage) => ChatMessage): void {
    this.messages.update((items) =>
      items.map((item) => (item.id === messageId ? updater(item) : item))
    );
  }

  private buildApiUrl(path: string): string {
    const suffix = path.startsWith('/') ? path : `/${path}`;
    let base = this.apiBase().trim() || '/api';
    base = base.replace(/\/+$/, '');

    if (!base.endsWith('/api')) {
      base = `${base}/api`;
    }

    return `${base}${suffix}`;
  }

  private createMessage(
    role: ChatRole,
    content: string,
    includeInContext = true,
    streaming = false,
    requestContent: MessagePayloadContent = content,
    attachments: ImageAttachment[] = []
  ): ChatMessage {
    return {
      id: Date.now() + Math.floor(Math.random() * 1000),
      role,
      content,
      time: new Intl.DateTimeFormat('en-US', {
        hour: '2-digit',
        minute: '2-digit'
      }).format(new Date()),
      includeInContext,
      streaming,
      attachments,
      requestContent
    };
  }
}
