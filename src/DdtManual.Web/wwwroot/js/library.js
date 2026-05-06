/**
 * DdT Manual — Your library
 * Saves page references to browser localStorage (same key as Service Manual for continuity).
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'dfe-sm-library';

    function trackGaEvent(eventName, properties) {
        if (typeof window.trackEvent === 'function') {
            window.trackEvent(eventName, properties || {});
            return;
        }

        window.dataLayer = window.dataLayer || [];
        window.dataLayer.push(Object.assign({ event: eventName }, properties || {}));
    }

    var DfeLibrary = {
        getAll: function () {
            try {
                return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]');
            } catch (e) {
                return [];
            }
        },

        has: function (id) {
            return this.getAll().some(function (item) { return item.id === id; });
        },

        add: function (item) {
            var items = this.getAll().filter(function (i) { return i.id !== item.id; });
            items.unshift(item);
            try { localStorage.setItem(STORAGE_KEY, JSON.stringify(items)); } catch (e) { /* storage full */ }
            this._emit();
        },

        remove: function (id) {
            var items = this.getAll().filter(function (item) { return item.id !== id; });
            try { localStorage.setItem(STORAGE_KEY, JSON.stringify(items)); } catch (e) { }
            this._emit();
        },

        toggle: function (item) {
            if (this.has(item.id)) { this.remove(item.id); } else { this.add(item); }
        },

        clear: function () {
            try { localStorage.removeItem(STORAGE_KEY); } catch (e) { }
            this._emit();
        },

        _emit: function () {
            document.dispatchEvent(new CustomEvent('dfe:library:changed'));
            updateHeaderLibraryCount();
        }
    };

    function escHtml(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escAttr(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function getLiveRegion() {
        var existing = document.getElementById('library-live-region');
        if (existing) { return existing; }

        var region = document.createElement('div');
        region.id = 'library-live-region';
        region.className = 'govuk-visually-hidden';
        region.setAttribute('role', 'status');
        region.setAttribute('aria-live', 'polite');
        region.setAttribute('aria-atomic', 'true');
        document.body.appendChild(region);
        return region;
    }

    function announceLibraryChange(message) {
        var region = getLiveRegion();
        region.textContent = '';
        window.setTimeout(function () {
            region.textContent = message;
        }, 20);
    }

    function getRemoveModal() {
        var existing = document.getElementById('lib-remove-modal');
        if (existing) { return existing; }

        var wrapper = document.createElement('div');
        wrapper.id = 'lib-remove-modal';
        wrapper.className = 'lib-modal';
        wrapper.hidden = true;
        wrapper.innerHTML =
            '<div class="lib-modal__backdrop" data-lib-modal-cancel></div>' +
            '<div class="lib-modal__dialog" role="dialog" tabindex="-1" aria-modal="true" aria-labelledby="lib-remove-modal-title" aria-describedby="lib-remove-modal-body">' +
            '<h2 class="govuk-heading-m lib-modal__title" id="lib-remove-modal-title">Remove saved item</h2>' +
            '<p class="govuk-body" id="lib-remove-modal-body"></p>' +
            '<div class="lib-modal__actions">' +
            '<button type="button" class="govuk-button govuk-button--warning" data-lib-modal-confirm>Remove</button>' +
            '<button type="button" class="govuk-button govuk-button--secondary" data-lib-modal-cancel>Cancel</button>' +
            '</div>' +
            '</div>';

        document.body.appendChild(wrapper);
        return wrapper;
    }

    function showLibraryConfirmDialog(options, onConfirm) {
        var modal = getRemoveModal();
        var dialog = modal.querySelector('.lib-modal__dialog');
        var title = modal.querySelector('#lib-remove-modal-title');
        var body = modal.querySelector('#lib-remove-modal-body');
        var confirmBtn = modal.querySelector('[data-lib-modal-confirm]');
        var cancelBtns = modal.querySelectorAll('[data-lib-modal-cancel]');
        var previousActiveElement = document.activeElement;

        if (title) {
            title.textContent = options.title || 'Confirm action';
        }
        if (body) {
            body.textContent = options.body || '';
        }
        if (confirmBtn) {
            confirmBtn.textContent = options.confirmText || 'Confirm';
        }

        function closeModal() {
            modal.hidden = true;
            document.body.classList.remove('lib-modal-open');
            confirmBtn.removeEventListener('click', confirmHandler);
            cancelBtns.forEach(function (btn) { btn.removeEventListener('click', cancelHandler); });
            document.removeEventListener('keydown', escHandler);
            if (previousActiveElement && typeof previousActiveElement.focus === 'function') {
                previousActiveElement.focus();
            }
        }

        function confirmHandler() {
            closeModal();
            onConfirm();
        }

        function cancelHandler() {
            closeModal();
        }

        function escHandler(e) {
            if (e.key === 'Escape') {
                e.preventDefault();
                closeModal();
            }
        }

        confirmBtn.addEventListener('click', confirmHandler);
        cancelBtns.forEach(function (btn) { btn.addEventListener('click', cancelHandler); });
        document.addEventListener('keydown', escHandler);

        modal.hidden = false;
        document.body.classList.add('lib-modal-open');
        if (dialog && typeof dialog.focus === 'function') {
            dialog.focus();
        }
    }

    function showRemoveConfirmDialog(itemTitle, onConfirm) {
        showLibraryConfirmDialog({
            title: 'Remove saved item',
            body: 'Are you sure you want to remove "' + itemTitle + '" from your library?',
            confirmText: 'Remove'
        }, onConfirm);
    }

    function updateBookmarkIcon(btn, saved) {
        var icon = btn.querySelector('.library-btn__icon');
        if (!icon) { return; }
        icon.textContent = saved ? 'bookmark_remove' : 'bookmark_add';
    }

    function updateButton(btn) {
        var id = btn.getAttribute('data-lib-id');
        var saved = DfeLibrary.has(id);
        var label = btn.querySelector('.library-btn__label');
        if (label) {
            label.textContent = saved ? 'Remove from library' : 'Add to library';
        }
        updateBookmarkIcon(btn, saved);
        btn.setAttribute('aria-pressed', saved ? 'true' : 'false');
        btn.classList.toggle('library-btn--saved', saved);
    }

    function updateHeaderLibraryCount() {
        var count = DfeLibrary.getAll().length;
        var badge = document.getElementById('library-count-badge');
        var text = document.getElementById('library-count-text');
        var link = document.querySelector('.gem-c-layout-super-navigation-header__library-item-link');
        if (badge) {
            badge.textContent = count;
        }
        if (text) {
            text.textContent = count;
        }
        if (link) {
            link.setAttribute('aria-label', 'Your library (' + count + ' items)');
        }
    }

    function initToggles() {
        document.querySelectorAll('[data-library-toggle]').forEach(function (btn) {
            updateButton(btn);
            if (btn._libWired) { return; }
            btn._libWired = true;
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var item = {
                    id: btn.getAttribute('data-lib-id'),
                    title: btn.getAttribute('data-lib-title'),
                    url: btn.getAttribute('data-lib-url'),
                    type: btn.getAttribute('data-lib-type'),
                    description: btn.getAttribute('data-lib-description') || ''
                };
                var wasSaved = DfeLibrary.has(item.id);

                DfeLibrary.toggle(item);

                announceLibraryChange(
                    wasSaved
                        ? item.title + ' removed from your library.'
                        : item.title + ' added to your library.'
                );

                trackGaEvent('save_to_library', {
                    action: wasSaved ? 'remove' : 'add',
                    item_id: item.id,
                    item_name: item.title,
                    item_url: item.url,
                    content_type: item.type,
                    page_path: window.location.pathname
                });

                updateButton(btn);
            });
        });
    }

    function updateNavBadge() {
        var count = DfeLibrary.getAll().length;
        document.querySelectorAll('a.lib-nav-link').forEach(function (link) {
            var badge = link.querySelector('.lib-count');
            if (count > 0) {
                if (!badge) {
                    badge = document.createElement('span');
                    badge.className = 'dfe-f-count-badge lib-count';
                    link.appendChild(badge);
                }
                badge.textContent = String(count);
                badge.setAttribute(
                    'aria-label',
                    count === 1 ? '1 saved page' : count + ' saved pages'
                );
            } else if (badge) {
                badge.remove();
            }
        });
    }

    function renderLibraryPage() {
        var container = document.getElementById('library-content');
        if (!container) { return; }

        var items = DfeLibrary.getAll();
        var groups = {};
        items.forEach(function (item) {
            var t = item.type || 'Other';
            if (!groups[t]) { groups[t] = []; }
            groups[t].push(item);
        });

        container.setAttribute('aria-busy', 'false');

        if (items.length === 0) {
            container.innerHTML =
                '<div class="lib-empty">' +
                '<p class="govuk-body govuk-!-margin-bottom-2">You have not saved any pages yet.</p>' +
                '<p class="govuk-body"><a href="/guidance" class="govuk-link">Browse guidance</a> to ' +
                'find content to save to your library.</p>' +
                '</div>';
            return;
        }

        var groupKeys = Object.keys(groups).sort();

        var html =
            '<section class="library-saved" aria-labelledby="library-saved-heading">' +
            '<h2 class="govuk-heading-m govuk-!-margin-bottom-2" id="library-saved-heading">Saved items</h2>' +
            '<p class="govuk-body-s govuk-!-margin-bottom-6 lib-meta">' +
            items.length + '\u00a0saved\u00a0item' + (items.length !== 1 ? 's' : '') +
            '\u2002\u2014\u2002<button type="button" id="lib-clear-all" class="govuk-link lib-text-btn">Clear all</button>' +
            '</p>';

        groupKeys.forEach(function (type) {
            html += '<h3 class="govuk-heading-m">' + escHtml(type) + '</h3>';
            html += '<ul class="lib-items">';
            groups[type].forEach(function (item) {
                html +=
                    '<li class="lib-card">' +
                    '<div class="lib-card__main">' +
                    '<a href="' + encodeURI(item.url) + '" class="govuk-link lib-card__title">' +
                    escHtml(item.title) + '</a>';
                if (item.description) {
                    html += '<p class="govuk-body-s lib-card__desc">' + escHtml(item.description) + '</p>';
                }
                html +=
                    '</div>' +
                    '<button type="button" class="library-btn library-btn--saved lib-card__remove" ' +
                    'data-lib-remove="' + escAttr(item.id) + '" ' +
                    'data-lib-remove-title="' + escAttr(item.title) + '" ' +
                    'aria-label="Remove ' + escAttr(item.title) + ' from library">' +
                    '<span class="material-symbols-sharp library-btn__icon" aria-hidden="true">bookmark_remove</span>' +
                    '<span class="library-btn__label">Remove from library</span>' +
                    '</button>' +
                    '</li>';
            });
            html += '</ul>';
        });

        html += '</section>';

        container.innerHTML = html;

        container.querySelectorAll('[data-lib-remove]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var itemId = btn.getAttribute('data-lib-remove');
                var itemTitle = btn.getAttribute('data-lib-remove-title') || 'this item';
                var item = DfeLibrary.getAll().find(function (savedItem) {
                    return savedItem.id === itemId;
                }) || null;
                showRemoveConfirmDialog(itemTitle, function () {
                    DfeLibrary.remove(itemId);

                    trackGaEvent('library_item_removed', {
                        item_id: itemId,
                        item_name: itemTitle,
                        item_url: item && item.url ? item.url : '',
                        content_type: item && item.type ? item.type : '',
                        page_path: window.location.pathname
                    });

                    announceLibraryChange(itemTitle + ' removed from your library.');
                    renderLibraryPage();
                    updateNavBadge();
                });
            });
        });

        var clearAllBtn = document.getElementById('lib-clear-all');
        if (clearAllBtn) {
            clearAllBtn.addEventListener('click', function () {
                var savedItems = DfeLibrary.getAll();
                var count = savedItems.length;
                showLibraryConfirmDialog({
                    title: 'Clear your library',
                    body: 'Are you sure you want to remove all ' + count + ' saved item' + (count !== 1 ? 's' : '') + ' from your library?',
                    confirmText: 'Clear all'
                }, function () {
                    DfeLibrary.clear();

                    trackGaEvent('library_cleared', {
                        item_count: count,
                        content_types: Array.from(new Set(savedItems.map(function (item) {
                            return item.type || 'Other';
                        }))).join(','),
                        page_path: window.location.pathname
                    });

                    announceLibraryChange('All saved items removed from your library.');
                    renderLibraryPage();
                });
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        initToggles();
        updateNavBadge();
        updateHeaderLibraryCount();
        renderLibraryPage();

        document.addEventListener('dfe:library:changed', function () {
            updateHeaderLibraryCount();
            updateNavBadge();
            document.querySelectorAll('[data-library-toggle]').forEach(updateButton);
            renderLibraryPage();
        });
    });

    window.DfeLibrary = DfeLibrary;
}());
