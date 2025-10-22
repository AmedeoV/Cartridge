// Handle image loading errors with fallback
document.addEventListener('DOMContentLoaded', function() {
    // Add error handler to all game card images
    const images = document.querySelectorAll('.game-card-image');
    
    images.forEach(img => {
        img.addEventListener('error', function() {
            // Try alternative GOG CDN formats
            const currentSrc = this.src;
            
            if (currentSrc && !this.dataset.fallbackAttempted) {
                this.dataset.fallbackAttempted = 'true';
                
                // Extract product ID from URL
                const match = currentSrc.match(/(\d+)_product/);
                if (match) {
                    const productId = match[1];
                    
                    // Try alternative formats
                    const alternatives = [
                        `https://images.gog-statics.com/${productId}.jpg`,
                        `https://images.gog.com/${productId}.jpg`,
                        `https://images.gog-statics.com/${productId}_product_card_v2_thumbnail_271.jpg`,
                        `https://images.gog-statics.com/${productId}_product_tile_117h.jpg`
                    ];
                    
                    // Try first alternative
                    if (alternatives.length > 0) {
                        this.dataset.alternativeIndex = '0';
                        this.src = alternatives[0];
                        return;
                    }
                }
            } else if (this.dataset.alternativeIndex) {
                // Try next alternative
                const currentSrc = this.src;
                const match = currentSrc.match(/(\d+)/);
                if (match) {
                    const productId = match[1];
                    const alternatives = [
                        `https://images.gog-statics.com/${productId}.jpg`,
                        `https://images.gog.com/${productId}.jpg`,
                        `https://images.gog-statics.com/${productId}_product_card_v2_thumbnail_271.jpg`,
                        `https://images.gog-statics.com/${productId}_product_tile_117h.jpg`
                    ];
                    
                    const nextIndex = parseInt(this.dataset.alternativeIndex) + 1;
                    if (nextIndex < alternatives.length) {
                        this.dataset.alternativeIndex = nextIndex.toString();
                        this.src = alternatives[nextIndex];
                        return;
                    }
                }
            }
            
            // All fallbacks failed, show placeholder
            this.style.opacity = '0.5';
            this.style.objectFit = 'contain';
            this.style.padding = '20px';
            // Use data URI for a simple game controller icon
            this.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyMDAiIGhlaWdodD0iMjAwIiB2aWV3Qm94PSIwIDAgMjAwIDIwMCI+PHJlY3Qgd2lkdGg9IjIwMCIgaGVpZ2h0PSIyMDAiIGZpbGw9IiMxNTI4NDAiLz48dGV4dCB4PSI1MCUiIHk9IjUwJSIgZm9udC1zaXplPSI2MCIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iPvCfjoY8L3RleHQ+PC9zdmc+';
        });
    });
});
