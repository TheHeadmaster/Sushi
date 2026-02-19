# -- Project information -----------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#project-information

project = 'Sushi'
author = 'TheHeadmaster'
version = release = '0.1'

# -- General configuration ------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#general-configuration

exclude_patterns = [
    '_build',
    'Thumbs.db',
    '.DS_Store',
]
extensions = [
    'myst_parser',
    # other extensions...
]
language = 'en'
master_doc = 'index'
pygments_style = 'sphinx'
source_suffix = { '.rst': 'restructuredtext', '.txt': 'markdown', '.md': 'markdown' }
templates_path = ['_templates']

# -- Options for HTML output ----------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-html-output

html_theme = 'alabaster'
html_static_path = ['_static']
