export default {
    mounted(el: HTMLElement) {
        const text = el.textContent
        const originalClasses = el.className
        if (!text) return

        const parts = text.split(' - ')

        if (parts.length !== 3) {
            return
        }
        const [showName, episodeNumber, episodeTitle] = parts

        const wrapper = document.createElement('div')
        wrapper.className = `${originalClasses} inline-flex items-center gap-1.5 overflow-hidden min-w-0 w-full`

        const showNameSpan = document.createElement('span')
        showNameSpan.className = 'shrink-0'
        showNameSpan.textContent = showName

        const dash1 = document.createElement('span')
        dash1.textContent = '-'

        const episodeNumberSpan = document.createElement('span')
        episodeNumberSpan.textContent = episodeNumber

        const dash2 = document.createElement('span')
        dash2.textContent = '-'

        const episodeTitleSpan = document.createElement('span')
        episodeTitleSpan.className = 'text-primary-content/50 truncate block'
        episodeTitleSpan.textContent = episodeTitle

        wrapper.appendChild(showNameSpan)
        wrapper.appendChild(dash1)
        wrapper.appendChild(episodeNumberSpan)
        wrapper.appendChild(dash2)
        wrapper.appendChild(episodeTitleSpan)

        el.textContent = ''
        el.appendChild(wrapper)
    }
}
