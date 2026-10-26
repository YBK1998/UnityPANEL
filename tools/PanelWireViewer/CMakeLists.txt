cmake_minimum_required(VERSION 3.20)
project(PanelWireViewer CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(imgui CONFIG REQUIRED)
find_package(glfw3 CONFIG REQUIRED)
find_package(OpenGL REQUIRED)
find_package(unofficial-sqlite3 CONFIG REQUIRED)
find_package(unofficial-imgui-node-editor CONFIG REQUIRED)

add_executable(PanelWireViewer
    src/main.cpp
    src/db.cpp
)

# Embed the application icon (Windows resource script).
if (WIN32)
    target_sources(PanelWireViewer PRIVATE resources/app.rc)
endif()

target_link_libraries(PanelWireViewer PRIVATE
    imgui::imgui
    glfw
    OpenGL::GL
    unofficial::sqlite3::sqlite3
    unofficial::imgui-node-editor::imgui-node-editor
)

if (MSVC)
    # Use the Windows subsystem so no console window pops up alongside the GUI.
    set_target_properties(PanelWireViewer PROPERTIES
        WIN32_EXECUTABLE TRUE
        LINK_FLAGS "/ENTRY:mainCRTStartup")
    target_compile_options(PanelWireViewer PRIVATE /utf-8)
endif()
