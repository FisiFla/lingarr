<template>
    <CardComponent title="Services">
        <template #description>
            Configure translation services in priority order. Jobs will fall through the chain if a
            service fails or exceeds its quota.
        </template>
        <template #content>
            <SaveNotification ref="saveNotification" />
            <div class="flex flex-col space-y-2">
                <ServiceChainSettings
                    @save="saveNotification?.show()"
                    @update:chain="chainServiceTypes = $event" />

                <div v-for="service in chainServiceTypes" :key="service">
                    <component
                        :is="getServiceConfig(service)"
                        v-if="getServiceConfig(service)"
                        @save="saveNotification?.show()" />
                </div>
            </div>

            <div v-if="hasAiService">
                <div class="flex flex-col gap-4">
                    <div class="flex flex-col space-x-2">
                        <span class="font-semibold">
                            Customize request template and prompts
                        </span>
                        Adjust the AI request body, system prompt and context for translations.
                    </div>
                    <ButtonComponent
                        variant="primary"
                        size="md"
                        @click="router.push({ name: 'request-template-settings' })">
                        Open Request Settings
                        <ArrowRight class="ml-1 mt-1 h-4 w-4" />
                    </ButtonComponent>
                </div>
            </div>

            <SourceAndTarget @save="saveNotification?.show()" />
        </template>
    </CardComponent>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { SERVICE_TYPE } from '@/ts'
import CardComponent from '@/components/common/CardComponent.vue'
import SaveNotification from '@/components/common/SaveNotification.vue'
import ServiceChainSettings from '@/components/features/settings/ServiceChainSettings.vue'
import LibreTranslateConfig from '@/components/features/settings/services/LibreTranslateConfig.vue'
import DeepLConfig from '@/components/features/settings/services/DeepLConfig.vue'
import AnthropicConfig from '@/components/features/settings/services/AnthropicConfig.vue'
import OpenAiConfig from '@/components/features/settings/services/OpenAiConfig.vue'
import LocalAiConfig from '@/components/features/settings/services/LocalAiConfig.vue'
import GeminiConfig from '@/components/features/settings/services/GeminiConfig.vue'
import DeepSeekConfig from '@/components/features/settings/services/DeepSeekConfig.vue'
import SourceAndTarget from '@/components/features/settings/SourceAndTarget.vue'
import ButtonComponent from '@/components/common/ButtonComponent.vue'
import ArrowRight from '@/components/icons/ArrowRight.vue'

const saveNotification = ref<InstanceType<typeof SaveNotification> | null>(null)
const router = useRouter()
const chainServiceTypes = ref<string[]>([])

const aiServiceTypes = [
    SERVICE_TYPE.ANTHROPIC,
    SERVICE_TYPE.DEEPSEEK,
    SERVICE_TYPE.GEMINI,
    SERVICE_TYPE.LOCALAI,
    SERVICE_TYPE.OPENAI
]

const hasAiService = computed(() =>
    chainServiceTypes.value.some((st) => aiServiceTypes.includes(st as any))
)

function getServiceConfig(serviceType: string) {
    switch (serviceType) {
        case SERVICE_TYPE.LIBRETRANSLATE:
            return LibreTranslateConfig
        case SERVICE_TYPE.OPENAI:
            return OpenAiConfig
        case SERVICE_TYPE.ANTHROPIC:
            return AnthropicConfig
        case SERVICE_TYPE.LOCALAI:
            return LocalAiConfig
        case SERVICE_TYPE.DEEPL:
            return DeepLConfig
        case SERVICE_TYPE.GEMINI:
            return GeminiConfig
        case SERVICE_TYPE.DEEPSEEK:
            return DeepSeekConfig
        default:
            return null
    }
}
</script>
