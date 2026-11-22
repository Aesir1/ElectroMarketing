/* eslint-disable */
/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import type { DocumentTypeDecoration } from '@graphql-typed-document-node/core';
export type ConfirmDocumentUploadInput = {
  id: string;
};

export type DeleteDocumentInput = {
  id: string;
};

export type DocumentStatus =
  | 'AVAILABLE'
  | 'PENDING';

export type PurgeQueueInput = {
  name: string;
};

export type RenameDocumentInput = {
  fileName: string;
  id: string;
};

export type ReplaceDocumentContentInput = {
  contentType: string;
  id: string;
  sizeBytes: number;
};

export type RequestDocumentUploadInput = {
  contentType: string;
  fileName: string;
  sizeBytes: number;
};

export type ViewerQueryVariables = Exact<{ [key: string]: never; }>;


export type ViewerQuery = { viewer: { email: string | null, permissions: Array<string> } };

export type DocumentsQueryVariables = Exact<{ [key: string]: never; }>;


export type DocumentsQuery = { documents: { nodes: Array<{ id: string, fileName: string, contentType: string, sizeBytes: number, status: DocumentStatus, createdAtUtc: string, downloadUrl: string | null }> | null } | null };

export type RequestDocumentUploadMutationVariables = Exact<{
  input: RequestDocumentUploadInput;
}>;


export type RequestDocumentUploadMutation = { requestDocumentUpload: { documentUploadPayload: { uploadUrl: string, document: { id: string } } | null, errors: Array<
      | { __typename: 'ForbiddenError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ConfirmDocumentUploadMutationVariables = Exact<{
  input: ConfirmDocumentUploadInput;
}>;


export type ConfirmDocumentUploadMutation = { confirmDocumentUpload: { document: { id: string, status: DocumentStatus } | null, errors: Array<
      | { __typename: 'ConflictError', message: string }
      | { __typename: 'NotFoundError', message: string }
    > | null } };

export type RenameDocumentMutationVariables = Exact<{
  input: RenameDocumentInput;
}>;


export type RenameDocumentMutation = { renameDocument: { document: { id: string } | null, errors: Array<
      | { __typename: 'NotFoundError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ReplaceDocumentContentMutationVariables = Exact<{
  input: ReplaceDocumentContentInput;
}>;


export type ReplaceDocumentContentMutation = { replaceDocumentContent: { documentUploadPayload: { uploadUrl: string } | null, errors: Array<
      | { __typename: 'NotFoundError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type DeleteDocumentMutationVariables = Exact<{
  input: DeleteDocumentInput;
}>;


export type DeleteDocumentMutation = { deleteDocument: { errors: Array<{ __typename: 'NotFoundError', message: string }> | null } };

export type QueuesQueryVariables = Exact<{ [key: string]: never; }>;


export type QueuesQuery = { queues: Array<{ name: string, messages: number, consumers: number }> };

export type PurgeQueueMutationVariables = Exact<{
  input: PurgeQueueInput;
}>;


export type PurgeQueueMutation = { purgeQueue: { unsignedInt: number | null } };

export class TypedDocumentString<TResult, TVariables>
  extends String
  implements DocumentTypeDecoration<TResult, TVariables>
{
  __apiType?: NonNullable<DocumentTypeDecoration<TResult, TVariables>['__apiType']>;
  private value: string;
  public __meta__?: Record<string, any> | undefined;

  constructor(value: string, __meta__?: Record<string, any> | undefined) {
    super(value);
    this.value = value;
    this.__meta__ = __meta__;
  }

  override toString(): string & DocumentTypeDecoration<TResult, TVariables> {
    return this.value;
  }
}

export const ViewerDocument = new TypedDocumentString(`
    query Viewer {
  viewer {
    email
    permissions
  }
}
    `) as unknown as TypedDocumentString<ViewerQuery, ViewerQueryVariables>;
export const DocumentsDocument = new TypedDocumentString(`
    query Documents {
  documents(first: 100) {
    nodes {
      id
      fileName
      contentType
      sizeBytes
      status
      createdAtUtc
      downloadUrl
    }
  }
}
    `) as unknown as TypedDocumentString<DocumentsQuery, DocumentsQueryVariables>;
export const RequestDocumentUploadDocument = new TypedDocumentString(`
    mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {
  requestDocumentUpload(input: $input) {
    documentUploadPayload {
      uploadUrl
      document {
        id
      }
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<RequestDocumentUploadMutation, RequestDocumentUploadMutationVariables>;
export const ConfirmDocumentUploadDocument = new TypedDocumentString(`
    mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {
  confirmDocumentUpload(input: $input) {
    document {
      id
      status
    }
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ConfirmDocumentUploadMutation, ConfirmDocumentUploadMutationVariables>;
export const RenameDocumentDocument = new TypedDocumentString(`
    mutation RenameDocument($input: RenameDocumentInput!) {
  renameDocument(input: $input) {
    document {
      id
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<RenameDocumentMutation, RenameDocumentMutationVariables>;
export const ReplaceDocumentContentDocument = new TypedDocumentString(`
    mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {
  replaceDocumentContent(input: $input) {
    documentUploadPayload {
      uploadUrl
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ReplaceDocumentContentMutation, ReplaceDocumentContentMutationVariables>;
export const DeleteDocumentDocument = new TypedDocumentString(`
    mutation DeleteDocument($input: DeleteDocumentInput!) {
  deleteDocument(input: $input) {
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<DeleteDocumentMutation, DeleteDocumentMutationVariables>;
export const QueuesDocument = new TypedDocumentString(`
    query Queues {
  queues {
    name
    messages
    consumers
  }
}
    `) as unknown as TypedDocumentString<QueuesQuery, QueuesQueryVariables>;
export const PurgeQueueDocument = new TypedDocumentString(`
    mutation PurgeQueue($input: PurgeQueueInput!) {
  purgeQueue(input: $input) {
    unsignedInt
  }
}
    `) as unknown as TypedDocumentString<PurgeQueueMutation, PurgeQueueMutationVariables>;