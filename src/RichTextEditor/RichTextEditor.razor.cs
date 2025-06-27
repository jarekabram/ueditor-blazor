using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RichTextEditor
{
    public partial class RichTextEditor : ComponentBase, IDisposable
    {
        [Inject] private IJSRuntime Js { get; set; }
        [Inject] private HttpClient Http { get; set; }

        [Parameter] public string Value { get; set; }
        [Parameter] public EventCallback<string> ValueChanged { get; set; }
        [Parameter] public string Html { get; set; }
        [Parameter] public EventCallback<string> HtmlChanged { get; set; }
        [Parameter] public string Height { get; set; }
        [Parameter] public string Width { get; set; }
        [Parameter] public string Placeholder { get; set; }
        [Parameter] public string Class { get; set; }
        [Parameter] public string Style { get; set; }
        [Parameter] public string UploadUrl { get; set; }
        [Parameter] public string ToolbarClass { get; set; }
        [Parameter] public string ToolbarStyle { get; set; }
        [Parameter] public Dictionary<string, object> Options { get; set; } = new();
        [Parameter] public EventCallback<string> OnFocus { get; set; }
        [Parameter] public EventCallback<string> OnBlur { get; set; }
        [Parameter] public EventCallback<string> OnEscPress { get; set; }
        [Parameter] public EventCallback<string> OnCtrlEnterPress { get; set; }
        [Parameter] public EventCallback<string> OnSelect { get; set; }

        protected ElementReference _ref;
        private InputFile _imageInput;
        private InputFile _fileInput;
        private List<string> _toolbarButtons = new() { "bold", "italic", "orderedList", "unorderedList", "link", "image", "file" };
        protected readonly string _id = Guid.NewGuid().ToString();

        protected override void OnInitialized()
        {
            Value ??= string.Empty;
            Html ??= string.Empty;
            Width ??= "100%";
            Height ??= "200px";
            Placeholder ??= string.Empty;
            UploadUrl ??= "/api/upload";
            Options ??= new Dictionary<string, object>();

            if (Options.TryGetValue("ToolbarButtons", out var tb) && tb is IEnumerable<string> buttons)
            {
                _toolbarButtons = new List<string>(buttons);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                await Js.InvokeVoidAsync("BlazorRichTextEditor.setDimensions", _ref, Width, Height);
                await Js.InvokeVoidAsync("BlazorRichTextEditor.setPlaceholder", _ref, Placeholder);
                if (!string.IsNullOrEmpty(Html))
                {
                    await Js.InvokeVoidAsync("BlazorRichTextEditor.setHtml", _ref, Html);
                }
            }
        }

        private async Task HandleInput()
        {
            Value = await Js.InvokeAsync<string>("BlazorRichTextEditor.getText", _ref);
            Html = await Js.InvokeAsync<string>("BlazorRichTextEditor.getHtml", _ref);

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(Value);
            }
            if (HtmlChanged.HasDelegate)
            {
                await HtmlChanged.InvokeAsync(Html);
            }
        }

        private async Task HandleBlur()
        {
            await HandleInput();
            if (OnBlur.HasDelegate)
            {
                await OnBlur.InvokeAsync(Value);
            }
        }

        private async Task HandleFocus()
        {
            if (OnFocus.HasDelegate)
            {
                await OnFocus.InvokeAsync(Value);
            }
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Escape" && OnEscPress.HasDelegate)
            {
                await OnEscPress.InvokeAsync(Value);
            }
            else if (e.Key == "Enter" && e.CtrlKey && OnCtrlEnterPress.HasDelegate)
            {
                await OnCtrlEnterPress.InvokeAsync(Value);
            }
        }

        private async Task HandleSelection()
        {
            if (OnSelect.HasDelegate)
            {
                await OnSelect.InvokeAsync(Value);
            }
        }

        private async Task ExecCommand(string command, string arg = null)
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.execCommand", _ref, command, arg);
            await HandleInput();
        }

        private async Task CreateLink()
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.createLink", _ref);
            await HandleInput();
        }

        private const long MaxFileSize = 1024 * 1024 * 15; // 15MB

        private async Task TriggerImageUpload()
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.triggerClick", _imageInput.Element);
        }

        private async Task TriggerFileUpload()
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.triggerClick", _fileInput.Element);
        }

        private async Task UploadImage(InputFileChangeEventArgs e)
        {
            if (e.FileCount == 0 || string.IsNullOrEmpty(UploadUrl))
                return;

            var file = e.File;
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(MaxFileSize);
            content.Add(new StreamContent(stream)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(file.ContentType)
                }
            }, "file", file.Name);

            var response = await Http.PostAsync(UploadUrl, content);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync();
            var url = ParseUrl(json);
            if (!string.IsNullOrEmpty(url))
            {
                await Js.InvokeVoidAsync("BlazorRichTextEditor.insertHtml", _ref, $"<img src='{url}' />");
                await HandleInput();
            }
        }

        private async Task UploadFile(InputFileChangeEventArgs e)
        {
            if (e.FileCount == 0 || string.IsNullOrEmpty(UploadUrl))
                return;

            var file = e.File;
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(MaxFileSize);
            content.Add(new StreamContent(stream)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(file.ContentType)
                }
            }, "file", file.Name);

            var response = await Http.PostAsync(UploadUrl, content);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync();
            var url = ParseUrl(json);
            if (!string.IsNullOrEmpty(url))
            {
                await Js.InvokeVoidAsync("BlazorRichTextEditor.insertHtml", _ref, $"<a href='{url}'>{file.Name}</a>");
                await HandleInput();
            }
        }

        private static string ParseUrl(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    return urlProp.GetString();
                }
            }
            catch
            {
            }
            return null;
        }

        public ValueTask<string> GetValueAsync()
        {
            return Js.InvokeAsync<string>("BlazorRichTextEditor.getText", _ref);
        }

        public ValueTask<string> GetHtmlAsync()
        {
            return Js.InvokeAsync<string>("BlazorRichTextEditor.getHtml", _ref);
        }

        public async Task SetValueAsync(string value)
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.setText", _ref, value);
            await HandleInput();
        }

        public async Task SetHtmlAsync(string html)
        {
            await Js.InvokeVoidAsync("BlazorRichTextEditor.setHtml", _ref, html);
            await HandleInput();
        }

        public async Task SetHeight(string value)
        {
            Height = value;
            await Js.InvokeVoidAsync("BlazorRichTextEditor.setDimensions", _ref, Width, Height);
        }

        public void Dispose()
        {
        }
    }
}
