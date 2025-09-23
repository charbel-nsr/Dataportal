$(function () {
    var wrapper = $('#appareilsMultiSelectWrapper');
    var select = wrapper.find('select');
    var container = $('#appareilsSelectDisplay');
    var dropdown = $('#appareilsSelectOptions');
    var input = $('#appareilsFilterInput');
    var detailsContainer = $('#appareilDetailsContainer');

    select.hide();

    // Build option checkboxes
    select.find('option').each(function () {
        var opt = $(this);
        if (!opt.val()) return;
        var id = 'appareil_' + opt.val();
        var item = $('<div class="form-check"></div>');
        var cb = $('<input type="checkbox" class="form-check-input">').attr('id', id).val(opt.val());
        var label = $('<label class="form-check-label"></label>').attr('for', id).text(opt.text());
        item.append(cb, label);
        dropdown.append(item);
    });

    // Apply existing selections
    select.find('option:selected').each(function () {
        var value = $(this).val();
        dropdown.find('input[value="' + value + '"]').prop('checked', true);
        addTag($(this).text(), value);
    });
    updateDetails();

    container.on('click', function (e) {
        if (!$(e.target).is('#appareilsFilterInput') && !$(e.target).hasClass('remove-tag')) {
            dropdown.toggle();
            input.focus();
        }
    });

    // Close dropdown when clicking outside
    $(document).on('click', function (e) {
        if (!$(e.target).closest(wrapper).length) {
            dropdown.hide();
        }
    });

    input.on('input', function () {
        var term = $(this).val().toLowerCase();
        dropdown.show();
        dropdown.find('.form-check').each(function () {
            var label = $(this).find('label').text().toLowerCase();
            $(this).toggle(label.indexOf(term) !== -1);
        });
    });

    dropdown.on('change', 'input[type=checkbox]', function () {
        var value = $(this).val();
        var text = dropdown.find('label[for="' + $(this).attr('id') + '"]').text();
        if (this.checked) {
            select.find('option[value="' + value + '"]').prop('selected', true);
            if (!container.find('.tag[data-value="' + value + '"]').length) {
                addTag(text, value);
            }
            updateDetails();
        } else {
            select.find('option[value="' + value + '"]').prop('selected', false);
            container.find('.tag[data-value="' + value + '"]').remove();
            updateDetails();
        }
    });

    container.on('click', '.remove-tag', function (e) {
        var tag = $(this).parent();
        var value = tag.data('value');
        dropdown.find('input[value="' + value + '"]').prop('checked', false).trigger('change');
        e.stopPropagation();
        input.focus();
    });

    function updateDetails() {
        var selected = dropdown.find('input[type=checkbox]:checked');
        var existing = {};
        detailsContainer.find('.appareil-detail').each(function () {
            var id = $(this).data('id');
            existing[id] = {
                ident: $(this).find('input[name$="IdAppareilDansDonnees"]').val(),
                com: $(this).find('input[name$="Commentaire"]').val()
            };
        });
        detailsContainer.empty();
        selected.each(function (index) {
            var id = $(this).val();
            var name = dropdown.find('label[for="' + $(this).attr('id') + '"]').text();
            var data = existing[id] || {};
            var detail = $('<details class="appareil-detail mb-2" data-id="' + id + '"></details>');
            detail.append('<summary>' + name + '</summary>');
            var inner = $('<div class="mt-2"></div>');
            inner.append('<input type="hidden" name="AppareilInfos[' + index + '].IdAppareil" value="' + id + '" />');
            inner.append('<div class="form-floating mb-2"><input type="text" class="form-control" name="AppareilInfos[' + index + '].IdAppareilDansDonnees" value="' + (data.ident || '') + '" placeholder="Ids dans les données"/><label>Ids dans les données (séparés par des virgules)</label></div>');
            inner.append('<div class="form-floating mb-2"><input type="text" class="form-control" name="AppareilInfos[' + index + '].Commentaire" value="' + (data.com || '') + '" placeholder="Commentaire"/><label>Commentaire</label></div><hr>');
            detail.append(inner);
            detailsContainer.append(detail);
        });
    }

    updateDetails();

    function addTag(text, value) {
        var tag = $('<span class="tag badge bg-secondary me-1 mb-1" data-value="' + value + '">' + text + '<span class="remove-tag ms-1" role="button">&times;</span></span>');
        input.before(tag);
        input.val('');
    }
});