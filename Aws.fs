module Aws

open System.Collections.Generic
open PulumiSchema
open PulumiSchema.Types

let inferAncestorTypes(schemaVersion: string) =

    let knownAncestorTypes = Map.ofList [
        "aws:lb/loadBalancer:LoadBalancer", [
            "aws:ec2/vpc:Vpc"
            "aws:ec2/securityGroup:SecurityGroup"
        ]

        "aws:lb/listener:Listener", [
            "aws:lb/targetGroup:TargetGroup"
        ]
    ]

    let ancestorTypesByProperty = Map.ofList [
        "vpcId", "aws:ec2/vpc:Vpc"
        "loadBalancerArn", "aws:lb/loadBalancer:LoadBalancer"
        "securityGroups", "aws:ec2/securityGroup:SecurityGroup"
    ]

    match SchemaLoader.FromPulumi("aws", schemaVersion) with
    | Error error ->
        failwith $"Error while loading AWS schema version {schemaVersion}: {error}"
    | Ok schema ->
        let ancestorsByType = Dictionary<string, ResizeArray<string>>()
        for (resourceType, resourceSchema) in Map.toList schema.resources do
            let ancestors = ResizeArray<string>()
            match Map.tryFind resourceType knownAncestorTypes with
            | Some ancestorTypes ->
                ancestors.AddRange ancestorTypes
            | None -> ()

            for (propertyName, propertySchema) in Map.toList resourceSchema.properties do
                match Map.tryFind propertyName ancestorTypesByProperty with
                | Some ancestorType ->
                    ancestors.Add ancestorType
                | None -> ()

            if ancestors.Count > 0 then
                let distinctAncestors = ancestors |> Seq.distinct |> ResizeArray
                ancestorsByType.Add(resourceType, distinctAncestors)

        ancestorsByType

let generateLookupModule(schemaVersion) =
    let ancestorsByType = inferAncestorTypes schemaVersion
    let moduleBuilder = System.Text.StringBuilder()
    let append (line: string) = moduleBuilder.AppendLine line |> ignore
    append "// This file is auto generated using the build project, do not edit this file directly."
    append "// To regenerate it, run `dotnet run GenerateAwsAncestorTypes` in the root of the repository."
    append "[<RequireQualifiedAccess>]"
    append "module AwsAncestorTypes"
    append ""
    append "let ancestorsByType = Map.ofList ["
    for pair in ancestorsByType do
        let resourceType = pair.Key
        let ancestorTypes = pair.Value

        append $"    \"{resourceType}\", ["
        for ancestorType in ancestorTypes do
            append $"        \"{ancestorType}\""
        append "    ]"
    append "]"
    moduleBuilder.ToString()