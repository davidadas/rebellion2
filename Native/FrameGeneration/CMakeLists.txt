cmake_minimum_required(VERSION 3.24)

project(UnityPlayerFrameGenerationProxy LANGUAGES CXX)

add_library(UnityPlayerFrameGenerationProxy SHARED UnityPlayerProxy.cpp)
target_compile_features(UnityPlayerFrameGenerationProxy PRIVATE cxx_std_20)
target_compile_definitions(UnityPlayerFrameGenerationProxy PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)

if(MSVC)
    set_property(
        TARGET UnityPlayerFrameGenerationProxy
        PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    )
endif()

add_custom_command(
    TARGET UnityPlayerFrameGenerationProxy
    POST_BUILD
    COMMAND
        ${CMAKE_COMMAND} -E make_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/Bootstrap"
    COMMAND
        ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:UnityPlayerFrameGenerationProxy>"
        "${CMAKE_CURRENT_SOURCE_DIR}/Bootstrap/UnityPlayerFrameGenerationProxy.dll"
)
