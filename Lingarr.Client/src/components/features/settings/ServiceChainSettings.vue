<template>
    <div class="flex flex-col space-y-3">
        <span class="font-semibold">Service priority (drag to reorder):</span>

        <!-- Active chain -->
        <div class="flex flex-col space-y-1">
            <div
                v-for="(item, index) in activeServices"
                :key="item.serviceType"
                draggable="true"
                class="flex items-center justify-between rounded border border-accent/30 bg-accent/10 p-2 cursor-move"
                @dragstart="dragStart(index)"
                @dragover.prevent="dragOver(index)"
                @drop="drop(index)">
                <div class="flex items-center space-x-2">
                    <span class="text-sm font-medium">{{ index + 1 }}.</span>
                    <span>{{ getLabel(item.serviceType) }}</span>
                </div>
                <div class="flex items-center space-x-2">
                    <input
                        type="number"
                        :value="item.monthlyLimitChars"
                        :placeholder="'unlimited'"
                        class="w-32 rounded border border-accent/30 bg-secondary px-2 py-1 text-sm"
                        @change="updateLimit(index, ($event.target as HTMLInputElement).value)" />
                    <span class="text-xs opacity-60">chars/mo</span>
                    <div
                        v-if="item.monthlyLimitChars"
                        class="h-1.5 w-20 rounded-full bg-accent/20 overflow-hidden">
                        <div
                            class="h-full rounded-full transition-all"
                            :class="usagePercent(item) > 90 ? 'bg-red-500' : 'bg-green-500'"
                            :style="{ width: Math.min(usagePercent(item), 100) + '%' }">
                        </div>
                    </div>
                    <span v-if="item.monthlyLimitChars" class="text-xs opacity-60">
                        {{ formatChars(item.charsUsed) }}/{{ formatChars(item.monthlyLimitChars) }}
                    </span>
                    <button
                        class="ml-2 text-red-400 hover:text-red-300 text-sm"
                        @click="removeService(index)">
                        Remove
                    </button>
                </div>
            </div>
        </div>

        <!-- Available services -->
        <div v-if="availableServices.length > 0" class="flex flex-col space-y-1">
            <span class="text-sm opacity-60">Available services:</span>
            <div class="flex flex-wrap gap-2">
                <button
                    v-for="service in availableServices"
                    :key="service"
                    class="rounded border border-accent/30 px-3 py-1 text-sm hover:bg-accent/20"
                    @click="addService(service)">
                    + {{ getLabel(service) }}
                </button>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { SERVICE_TYPE } from '@/ts'
import {
    serviceChainService,
    type ServiceChainItem,
    type ServiceChainEntry
} from '@/services/serviceChainService'

const emit = defineEmits<{ save: []; 'update:chain': [string[]] }>()

const activeServices = ref<ServiceChainItem[]>([])
const dragIndex = ref<number | null>(null)

const allServiceTypes = Object.values(SERVICE_TYPE)
const serviceLabels: Record<string, string> = {
    anthropic: 'Anthropic',
    bing: 'Bing',
    deepl: 'DeepL',
    deepseek: 'DeepSeek',
    gemini: 'Gemini',
    google: 'Google',
    libretranslate: 'LibreTranslate',
    localai: 'Local AI',
    microsoft: 'Microsoft',
    openai: 'OpenAI',
    yandex: 'Yandex'
}

const availableServices = computed(() =>
    allServiceTypes.filter(
        (st) => !activeServices.value.some((a) => a.serviceType === st)
    )
)

function getLabel(serviceType: string): string {
    return serviceLabels[serviceType] ?? serviceType
}

function usagePercent(item: ServiceChainItem): number {
    if (!item.monthlyLimitChars || item.monthlyLimitChars === 0) return 0
    return (item.charsUsed / item.monthlyLimitChars) * 100
}

function formatChars(chars: number): string {
    if (chars >= 1_000_000) return (chars / 1_000_000).toFixed(1) + 'M'
    if (chars >= 1_000) return (chars / 1_000).toFixed(0) + 'K'
    return chars.toString()
}

function dragStart(index: number) {
    dragIndex.value = index
}

function dragOver(index: number) {
    if (dragIndex.value === null || dragIndex.value === index) return
    const item = activeServices.value.splice(dragIndex.value, 1)[0]
    activeServices.value.splice(index, 0, item)
    dragIndex.value = index
}

async function drop(_index: number) {
    dragIndex.value = null
    await saveChain()
}

function updateLimit(index: number, value: string) {
    const num = parseInt(value)
    activeServices.value[index].monthlyLimitChars = isNaN(num) || num <= 0 ? null : num
    saveChain()
}

function addService(serviceType: string) {
    activeServices.value.push({ serviceType, monthlyLimitChars: null, charsUsed: 0 })
    saveChain()
}

function removeService(index: number) {
    activeServices.value.splice(index, 1)
    saveChain()
}

async function saveChain() {
    const entries: ServiceChainEntry[] = activeServices.value.map((s) => ({
        serviceType: s.serviceType,
        monthlyLimitChars: s.monthlyLimitChars
    }))
    await serviceChainService.updateChain(entries)
    emit('update:chain', activeServices.value.map((s) => s.serviceType))
    emit('save')
}

onMounted(async () => {
    activeServices.value = await serviceChainService.getChain()
    emit('update:chain', activeServices.value.map((s) => s.serviceType))
})
</script>
