import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export interface TokenEvent {
  type: 'token'
  content: string
}

export interface ToolCallEvent {
  type: 'tool_call'
  name: string
  arguments: Record<string, unknown>
  result: string
}

export interface AgentStatusEvent {
  type: 'status'
  status: string
  message?: string
}

export interface MessageEvent {
  type: 'message'
  role: string
  content: string
}

export type StreamEvent = TokenEvent | ToolCallEvent | AgentStatusEvent | MessageEvent

export function useSignalR() {
  const connection = ref<signalR.HubConnection | null>(null)
  const isConnected = ref(false)
  const currentSessionId = ref<string | null>(null)

  const eventHandlers = new Map<string, Set<(...args: unknown[]) => void>>()

  async function connect() {
    if (connection.value) return

    connection.value = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.value.on('TokenStream', (token: string) => {
      emit('token', { type: 'token', content: token })
    })

    connection.value.on('ToolCallExecuted', (data: { name: string; arguments: Record<string, unknown>; result: string }) => {
      emit('toolCall', { type: 'tool_call', ...data })
    })

    connection.value.on('AgentStatusChanged', (data: { status: string; message?: string }) => {
      emit('status', { type: 'status', ...data })
    })

    connection.value.on('MessageReceived', (data: { role: string; content: string }) => {
      emit('message', { type: 'message', ...data })
    })

    connection.value.on('ErrorOccurred', (data: { message: string }) => {
      emit('error', { type: 'error', message: data.message })
    })

    connection.value.onreconnected(() => {
      isConnected.value = true
      if (currentSessionId.value) {
        connection.value?.invoke('JoinSession', currentSessionId.value)
      }
    })

    connection.value.onclose(() => {
      isConnected.value = false
    })

    try {
      await connection.value.start()
      isConnected.value = true
    } catch (err) {
      console.error('SignalR connection failed:', err)
      connection.value = null
    }
  }

  async function joinSession(sessionId: string) {
    if (!connection.value) await connect()
    currentSessionId.value = sessionId
    await connection.value?.invoke('JoinSession', sessionId)
  }

  async function leaveSession() {
    if (currentSessionId.value && connection.value) {
      await connection.value.invoke('LeaveSession', currentSessionId.value)
    }
    currentSessionId.value = null
  }

  function on(event: string, handler: (...args: unknown[]) => void) {
    if (!eventHandlers.has(event)) {
      eventHandlers.set(event, new Set())
    }
    eventHandlers.get(event)!.add(handler)
    return () => eventHandlers.get(event)?.delete(handler)
  }

  function emit(event: string, data: unknown) {
    eventHandlers.get(event)?.forEach(h => h(data))
  }

  async function disconnect() {
    await leaveSession()
    await connection.value?.stop()
    connection.value = null
    isConnected.value = false
  }

  onUnmounted(() => {
    disconnect()
  })

  return {
    connection,
    isConnected,
    currentSessionId,
    connect,
    joinSession,
    leaveSession,
    on,
    disconnect
  }
}
