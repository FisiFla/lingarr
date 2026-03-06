const escapeHtml = (text: string) =>
    text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

const mounted = (el: HTMLElement) => {
    const text = el.textContent || ''
    el.innerHTML = escapeHtml(text).replace(
        /`([^`]+)`/g,
        '<code class="px-1 py-0.5 rounded-sm bg-primary text-primary-content">$1</code>'
    )
}

export default { mounted }
