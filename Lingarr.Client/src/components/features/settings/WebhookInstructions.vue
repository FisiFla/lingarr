<template>
    <CardComponent title="Webhook">
        <template #description>
            Configure webhooks in Radarr and Sonarr to automatically trigger translations when new
            media is added.
        </template>
        <template #content>
            <div class="flex flex-col space-y-3">
                <span>
                    In Sonarr/Radarr go to
                    <b>Settings → Connect → + → Webhook</b>
                </span>

                <div class="flex flex-col space-y-1">
                    <span class="font-semibold">Radarr webhook URL</span>
                    <code class="bg-accent/20 mt-1 block rounded p-2 text-sm overflow-x-auto">
                        {{ webhookUrl }}/api/webhook/radarr
                    </code>
                </div>

                <div class="flex flex-col space-y-1">
                    <span class="font-semibold">Sonarr webhook URL</span>
                    <code class="bg-accent/20 mt-1 block rounded p-2 text-sm overflow-x-auto">
                        {{ webhookUrl }}/api/webhook/sonarr
                    </code>
                </div>

                <div class="flex flex-col space-y-1">
                    <span class="font-semibold">Authentication</span>
                    <span class="text-sm">
                        If authentication is enabled in Lingarr, add a custom header in the webhook
                        configuration. Leave <b>Username</b> and <b>Password</b> empty and add a
                        <b>Header</b> instead:
                    </span>
                    <code class="bg-accent/20 mt-1 block rounded p-2 text-sm overflow-x-auto">
                        X-Api-Key: <i>your-lingarr-api-key</i>
                    </code>
                    <span class="text-sm opacity-70">
                        You can generate an API key in Settings → Authentication → API Key.
                        If authentication is disabled, no header is needed.
                    </span>
                </div>
            </div>
        </template>
    </CardComponent>
</template>

<script setup lang="ts">
import CardComponent from '@/components/common/CardComponent.vue'

const webhookUrl = window.location.origin
</script>
